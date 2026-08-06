using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Presentation.Infrastructure
{
    /// <summary>
    /// Health check cho load balancer / docker / k8s probe.
    ///   /health/live  — tiến trình còn sống (không đụng tới DB, dùng cho liveness probe)
    ///   /health/ready — DB và Redis kết nối được (dùng cho readiness probe)
    /// Tách hai đường vì nếu liveness cũng kiểm tra DB thì DB chớp tắt một nhịp
    /// sẽ khiến orchestrator restart cả API — hỏng thêm chứ không cứu được gì.
    /// </summary>
    public static class HealthCheckSetup
    {
        private const string ReadyTag = "ready";

        public static IServiceCollection AddAppHealthChecks(
            this IServiceCollection services, IConfiguration config)
        {
            var builder = services.AddHealthChecks();

            var sqlConnection = config.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(sqlConnection))
            {
                builder.AddSqlServer(sqlConnection, name: "sqlserver", tags: [ReadyTag]);
            }

            var redisConnection = config.GetConnectionString("Redis");

            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                builder.AddRedis(redisConnection, name: "redis", tags: [ReadyTag]);
            }

            return services;
        }

        public static void MapAppHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                // Không chạy check nào — chỉ cần trả 200 là biết tiến trình còn sống.
                Predicate = _ => false
            }).DisableRateLimiting();

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(ReadyTag),
                ResponseWriter = WriteResponseAsync
            }).DisableRateLimiting();
        }

        private static Task WriteResponseAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = new
            {
                status = report.Status.ToString(),
                totalDurationMs = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    // Chỉ lộ message lỗi ở Development — production không để lộ
                    // chuỗi kết nối hay chi tiết hạ tầng ra ngoài.
                    error = context.RequestServices
                        .GetRequiredService<IHostEnvironment>().IsDevelopment()
                        ? e.Value.Exception?.Message
                        : null
                })
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
