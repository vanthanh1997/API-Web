using Application.Common.Interfaces;
using Application.Common.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Presentation.Infrastructure
{
    public class TokenBlacklistMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, ITokenBlacklist blacklist)
        {
            // Chưa đăng nhập thì không có token để kiểm tra.
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await next(context);
                return;
            }

            var jti = context.User.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Token không có jti (token lạ / do hệ thống khác phát) thì bỏ qua bước này;
            // việc xác thực chữ ký đã do JwtBearer làm trước đó.
            if (string.IsNullOrEmpty(jti))
            {
                await next(context);
                return;
            }

            var issuedAt = GetIssuedAt(context.User);

            if (await blacklist.IsRevokedAsync(jti, userId, issuedAt, context.RequestAborted))
            {
                await WriteRevokedResponseAsync(context);
                return;
            }

            await next(context);
        }

        /// <summary>
        /// Lấy thời điểm phát token từ claim "iat". Không có thì trả UtcNow — nghĩa là
        /// coi như token vừa phát, nên KHÔNG bị chặn bởi mốc "thu hồi toàn bộ".
        /// Chọn mặc định này để tránh chặn oan token hợp lệ chỉ vì thiếu claim.
        /// </summary>
        private static DateTimeOffset GetIssuedAt(ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(JwtRegisteredClaimNames.Iat);

            return long.TryParse(raw, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : DateTimeOffset.UtcNow;
        }

        private static async Task WriteRevokedResponseAsync(HttpContext context)
        {
            var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";

            // Cùng khuôn ApiResponse như mọi lỗi khác để client chỉ xử lý một định dạng.
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(
                    "Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.", traceId),
                context.RequestAborted);
        }
    }

    public static class TokenBlacklistMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenBlacklist(this IApplicationBuilder app)
            => app.UseMiddleware<TokenBlacklistMiddleware>();
    }
}
