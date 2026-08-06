using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace Presentation.Infrastructure
{
    public class ApiResponseWrapperFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            context.Result = context.Result switch
            {
                // Đã bọc sẵn rồi thì thôi (tránh bọc 2 lần).
                ObjectResult o when IsWrapped(o.Value) => o,

                // Lỗi: giữ nguyên, đã có khuôn riêng.
                ObjectResult o when o.Value is ProblemDetails => o,
                ObjectResult { StatusCode: >= 400 } o => o,

                // CreatedAtActionResult PHẢI đứng trước ObjectResult vì nó kế thừa ObjectResult.
                // Giữ header Location, chỉ bọc phần body.
                CreatedAtActionResult c => new CreatedAtActionResult(
                    c.ActionName, c.ControllerName, c.RouteValues, Wrap(c.Value, traceId)),

                CreatedResult c => new CreatedResult(c.Location ?? string.Empty, Wrap(c.Value, traceId)),

                // 200: bọc giá trị vào Data.
                ObjectResult o => new ObjectResult(Wrap(o.Value, traceId))
                {
                    StatusCode = o.StatusCode ?? StatusCodes.Status200OK
                },

                // 204 NoContent -> đổi thành 200 kèm body, để client luôn đọc được cùng khuôn.
                NoContentResult => new ObjectResult(ApiResponse<object>.Ok(null, traceId))
                {
                    StatusCode = StatusCodes.Status200OK
                },

                _ => context.Result
            };

            await next();
        }

        private static bool IsWrapped(object? value)
            => value?.GetType() is { IsGenericType: true } t
               && t.GetGenericTypeDefinition() == typeof(ApiResponse<>);

        private static object Wrap(object? value, string traceId)
        {
            var type = typeof(ApiResponse<>).MakeGenericType(value?.GetType() ?? typeof(object));

            return type.GetMethod(nameof(ApiResponse<object>.Ok))!
                .Invoke(null, [value, traceId, null])!;
        }
    }
}
