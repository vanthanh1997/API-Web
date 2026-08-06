using Serilog.Context;

namespace Presentation.Infrastructure
{
    public class CorrelationIdMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-Correlation-Id";
        private const string LogPropertyName = "CorrelationId";

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreate(context);
            context.Items[HeaderName] = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });
            using (LogContext.PushProperty(LogPropertyName, correlationId))
            {
                await next(context);
            }
        }

        private static string GetOrCreate(HttpContext context)
        {
            var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

            // Chặn độ dài: header do client gửi nên coi là dữ liệu không tin cậy —
            // id dài 10 MB sẽ phình mọi dòng log và có thể làm nghẽn hệ thống log.
            if (!string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 128)
            {
                return incoming;
            }

            return Guid.NewGuid().ToString("N");
        }
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
            => app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
