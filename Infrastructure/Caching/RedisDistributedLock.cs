using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Caching
{
    public class RedisDistributedLock(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedLock> logger) : IDistributedLock
    {
        private const string KeyPrefix = "lock:";

        public async Task<IAsyncDisposable?> TryAcquireAsync(
            string key, TimeSpan ttl, CancellationToken ct)
        {
            var db = redis.GetDatabase();
            var fullKey = KeyPrefix + key;

            // Token nhận dạng chủ sở hữu khoá lần này.
            var token = Guid.NewGuid().ToString("N");

            // When.NotExists = NX: chỉ đặt được nếu khoá chưa có ai giữ.
            var acquired = await db.StringSetAsync(fullKey, token, ttl, When.NotExists);

            if (!acquired)
            {
                logger.LogDebug("Không giành được khoá {Key} — tiến trình khác đang giữ.", key);
                return null;
            }

            logger.LogDebug("Đã giành khoá {Key} trong {Ttl}.", key, ttl);

            return new RedisLockHandle(db, fullKey, token, logger);
        }

        /// <summary>
        /// Handle giải phóng khoá. Dùng với <c>await using</c> để chắc chắn khoá được nhả
        /// kể cả khi thân lệnh ném exception.
        /// </summary>
        private sealed class RedisLockHandle(
            IDatabase db, string key, string token, ILogger logger) : IAsyncDisposable
        {
            /// <summary>
            /// Script Lua để "so giá trị rồi mới xoá" trong MỘT lệnh nguyên tử.
            /// Làm bằng hai lệnh Get + Delete riêng sẽ có khe hở: giữa hai lệnh, khoá có
            /// thể hết hạn và được tiến trình khác giành — lúc đó Delete xoá mất khoá của họ.
            /// </summary>
            private const string ReleaseScript = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end
                """;

            public async ValueTask DisposeAsync()
            {
                try
                {
                    var released = (int)(long)await db.ScriptEvaluateAsync(
                        ReleaseScript, [key], [token]);

                    if (released == 0)
                    {
                        // Khoá đã hết hạn trước khi công việc xong -> có thể đã có tiến trình
                        // khác vào cùng đoạn việc. Đây là dấu hiệu TTL đặt quá ngắn.
                        logger.LogWarning(
                            "Khoá {Key} không còn thuộc tiến trình này khi giải phóng — " +
                            "TTL có thể quá ngắn so với thời gian chạy thực tế.", key);
                    }
                }
                catch (Exception ex)
                {
                    // Không để việc nhả khoá làm vỡ luồng nghiệp vụ đã hoàn thành:
                    // khoá vẫn tự hết hạn theo TTL.
                    logger.LogError(ex, "Lỗi khi giải phóng khoá {Key}.", key);
                }
            }
        }
    }
}
