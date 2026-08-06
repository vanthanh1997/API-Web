namespace Presentation.Infrastructure
{
    public class SecurityHeadersMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Chặn browser "đoán" kiểu nội dung khác với Content-Type khai báo.
            // Không có nó, response JSON chứa mã HTML có thể bị browser thực thi như HTML.
            headers["X-Content-Type-Options"] = "nosniff";

            // Chặn nhúng trang này vào iframe của site khác -> chống clickjacking.
            headers["X-Frame-Options"] = "DENY";

            // Không gửi URL đầy đủ (có thể chứa token trong query) sang site khác.
            headers["Referrer-Policy"] = "no-referrer";

            // Tắt các API thiết bị mà API backend không bao giờ cần.
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            // CSP: chỉ định nguồn tài nguyên được phép tải.
            // 'unsafe-inline' cho style/script là để Swagger UI hoạt động — nếu môi trường
            // production KHÔNG bật Swagger thì nên siết lại thành "default-src 'none'".
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data:; " +
                    "font-src 'self' data:; " +
                    "connect-src 'self'; " +
                    // Chặn chính trang này bị nhúng iframe (bản CSP của X-Frame-Options).
                    "frame-ancestors 'none'; " +
                    "base-uri 'self'; " +
                    "form-action 'self'";
            }

            // Xoá header tiết lộ công nghệ/phiên bản server — thông tin này chỉ giúp
            // kẻ tấn công chọn đúng lỗ hổng để thử.
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");

            await next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
