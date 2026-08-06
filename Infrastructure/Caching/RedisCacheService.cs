using Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Infrastructure.Caching
{
    public class RedisCacheService(IDistributedCache cache) : ICacheService
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);
        public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
        {
            var json = await cache.GetStringAsync(key, ct);

            return json is null ? default : JsonSerializer.Deserialize<T>(json);
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl, CancellationToken ct)
        {
            var cached = await GetAsync<T>(key, ct);

            if (cached is not null) return cached;

            var value = await factory();
            await SetAsync(key, value, ttl, ct);

            return value;
        }

        public Task RemoveAsync(string key, CancellationToken ct)
         => cache.RemoveAsync(key, ct);

        public Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct)
       => cache.SetStringAsync(
           key,
           JsonSerializer.Serialize(value),
           new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl },
           ct);
    }
}
