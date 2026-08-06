using Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
namespace Infrastructure.Caching
{
    public class RedisTokenBlacklist(IDistributedCache cache) : ITokenBlacklist
    {
        // Tiền tố khoá để không lẫn với dữ liệu cache khác trong cùng Redis.
        private const string TokenPrefix = "revoked-token:";
        private const string UserPrefix = "revoked-user:";

        public Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken ct)
        {
            var ttl = expiresAt - DateTimeOffset.UtcNow;

            // Token đã hết hạn thì tự vô hiệu, không cần ghi gì (và TTL âm sẽ lỗi).
            if (ttl <= TimeSpan.Zero) return Task.CompletedTask;

            return cache.SetStringAsync(
                TokenPrefix + jti,
                "1",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);
        }

        public Task RevokeAllForUserAsync(string userId, DateTimeOffset until, CancellationToken ct)
        {
            // Ghi MỐC THỜI GIAN thay vì liệt kê từng token: ta không biết user đang có bao
            // nhiêu token đang sống. Mọi token phát TRƯỚC mốc này bị coi là không hợp lệ.
            //
            // TTL đặt 24 giờ — dài hơn tuổi thọ tối đa của access token (15 phút) rất nhiều
            // để chắc chắn không có token nào "sống sót" qua mốc, nhưng vẫn tự dọn.
            return cache.SetStringAsync(
                UserPrefix + userId,
                until.ToUnixTimeSeconds().ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                ct);
        }

        public async Task<bool> IsRevokedAsync(
            string jti, string? userId, DateTimeOffset issuedAt, CancellationToken ct)
        {
            // 1. Token này có bị thu hồi riêng lẻ không?
            if (await cache.GetStringAsync(TokenPrefix + jti, ct) is not null)
            {
                return true;
            }

            if (string.IsNullOrEmpty(userId)) return false;

            // 2. Toàn bộ token của user có bị thu hồi không (khoá tài khoản / đổi role)?
            var raw = await cache.GetStringAsync(UserPrefix + userId, ct);

            if (raw is null || !long.TryParse(raw, out var cutoffUnix)) return false;

            // Token phát TRƯỚC mốc thu hồi -> không còn hợp lệ.
            // Dùng <= thay vì < : token phát đúng giây thu hồi cũng phải bị chặn.
            return issuedAt.ToUnixTimeSeconds() <= cutoffUnix;
        }
    }
}
