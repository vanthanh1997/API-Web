using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
namespace Infrastructure.Caching
{
    public class NoOpDistributedLock(ILogger<NoOpDistributedLock> logger) : IDistributedLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
        {
            logger.LogWarning(
                "Cấp khoá {Key} mà KHÔNG có khoá thật: chưa cấu hình ConnectionStrings:Redis. " +
                "Chỉ an toàn khi hệ thống chạy một tiến trình duy nhất.", key);

            return Task.FromResult<IAsyncDisposable?>(new NoOpHandle());
        }

        private sealed class NoOpHandle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
