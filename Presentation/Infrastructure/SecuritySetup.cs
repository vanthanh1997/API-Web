using Application.Common.Models;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Presentation.Infrastructure
{
    public static class SecuritySetup
    {
        public const string CorsPolicy = "Frontend";
        public const string AuthLimiter = "auth";

        /// <summary>
        /// CORS: chỉ cho phép đúng domain frontend đã khai trong appsettings.
        /// KHÔNG dùng AllowAnyOrigin vì còn phải gửi Authorization header.
        /// </summary>
        public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration config)
        {
            var origins = config.GetSection("AppUrls:AllowedOrigins").Get<string[]>() ?? [config["AppUrls:Frontend"] ?? "http://localhost:3000"];
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Location")));
            return services;
        }

        /// <summary>
        /// Rate limit 2 mức: API thường 100 req/phút/IP, còn login/register 5 req/phút/IP
        /// để chống dò mật khẩu.
        ///
        /// Hai hạn mức đọc từ cấu hình (<c>RateLimit:GlobalPermitLimit</c> và
        /// <c>RateLimit:AuthPermitLimit</c>) để điều chỉnh được theo môi trường mà không
        /// phải sửa code — production có thể nới/thắt tuỳ lượng truy cập, và integration
        /// test nâng hạn mức lên vì cả bộ test dùng chung một IP nên rất nhanh bị 429.
        /// Không khai thì giữ đúng mặc định an toàn 100 / 5.
        /// </summary>
        public static IServiceCollection AddRateLimiting(
            this IServiceCollection services, IConfiguration config)
        {
            var globalLimit = config.GetValue<int?>("RateLimit:GlobalPermitLimit") ?? 100;
            var authLimit = config.GetValue<int?>("RateLimit:AuthPermitLimit") ?? 5;

            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ClientKey(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = globalLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                options.AddPolicy(AuthLimiter, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ClientKey(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authLimit,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                options.OnRejected = async (context, ct) =>
                {
                    var traceId = context.HttpContext.TraceIdentifier;

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        ApiResponse<object>.Fail(
                            "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.", traceId), ct);
                };
            });

            return services;
        }
        /// <summary>
        /// Đếm theo user nếu đã đăng nhập, chưa thì theo IP.
        /// Sau reverse proxy nhớ bật UseForwardedHeaders, nếu không mọi request sẽ chung 1 IP.
        /// </summary>
        private static string ClientKey(HttpContext context)
        {
            // Dùng NameIdentifier chứ KHÔNG dùng Identity.Name: token do JwtTokenService
            // phát ra không có claim "name", nên Identity.Name luôn null và mọi user
            // đăng nhập sẽ rơi chung một partition — một người spam là cả hệ thống bị chặn.
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return userId is not null
                ? $"user:{userId}"
                : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        }
    }
}
