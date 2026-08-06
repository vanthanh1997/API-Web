using Application.Common.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs
{
    public class ExpiredTokenCleanupService(
       IServiceScopeFactory scopeFactory,
       ILogger<ExpiredTokenCleanupService> logger,
       TimeProvider clock) : BackgroundService
    {
        /// <summary>Khoảng cách giữa hai lần dọn. 6 giờ là đủ: đây không phải việc gấp.</summary>
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        /// <summary>
        /// Giữ token đã thu hồi thêm 7 ngày để còn dấu vết điều tra "token bị dùng lại sau
        /// rotation" (dấu hiệu bị đánh cắp) — khớp với luật trong IdentityService.
        /// </summary>
        private static readonly TimeSpan RevokedGracePeriod = TimeSpan.FromDays(7);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Chờ một nhịp trước lần chạy đầu: lúc khởi động, ứng dụng còn đang migrate DB
            // và nạp cache — không nên thêm việc nặng vào đúng thời điểm đó.
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;   // app tắt ngay khi vừa bật
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;   // app đang tắt — không phải lỗi
                }
                catch (Exception ex)
                {
                    // BẮT BUỘC bắt hết: exception thoát khỏi ExecuteAsync sẽ làm
                    // BackgroundService dừng vĩnh viễn (và ở .NET 6+ có thể hạ cả host).
                    // Một lần dọn lỗi không đáng để mất luôn việc dọn định kỳ.
                    logger.LogError(ex, "Lỗi khi dọn refresh token hết hạn. Sẽ thử lại sau {Interval}.", Interval);
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CleanupOnceAsync(CancellationToken ct)
        {
            // Scope riêng cho mỗi lần chạy: BackgroundService là singleton, không được giữ
            // DbContext (scoped) suốt đời ứng dụng — DbContext không thread-safe và sẽ
            // tích luỹ change tracker đến mức rò rỉ bộ nhớ.
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var distributedLock = sp.GetRequiredService<IDistributedLock>();

            // TTL 10 phút: dài hơn thời gian dọn thực tế rất nhiều, nhưng vẫn tự nhả nếu
            // tiến trình chết giữa đường.
            await using var handle = await distributedLock.TryAcquireAsync(
                "cleanup-expired-refresh-tokens", TimeSpan.FromMinutes(10), ct);

            if (handle is null)
            {
                logger.LogDebug("Instance khác đang dọn refresh token — bỏ qua lượt này.");
                return;
            }

            var db = sp.GetRequiredService<AppDbContext>();

            var now = clock.GetUtcNow();
            var revokedCutoff = now - RevokedGracePeriod;

            // ExecuteDeleteAsync sinh MỘT câu DELETE, không nạp entity vào bộ nhớ —
            // quan trọng khi bảng có hàng trăm nghìn dòng chết.
            var deleted = await db.RefreshTokens
                .Where(t => t.ExpiresAt <= now || t.RevokedAt <= revokedCutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                logger.LogInformation("Đã dọn {Count} refresh token đã chết.", deleted);
            }
        }
    }
}
