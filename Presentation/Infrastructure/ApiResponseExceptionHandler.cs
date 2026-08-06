using Application.Common.Exceptions;
using Application.Common.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Presentation.Infrastructure
{
    public class ApiResponseExceptionHandler(ILogger<ApiResponseExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
       HttpContext context, Exception exception, CancellationToken ct)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            var (status, message, errors) = exception switch
            {
                ValidationException ex => (
                    StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.", ex.Errors),

                NotFoundException ex => (
                    StatusCodes.Status404NotFound, ex.Message, null),

                ConflictException ex => (
                    StatusCodes.Status409Conflict, ex.Message, null),

                ForbiddenAccessException ex => (
                    StatusCodes.Status403Forbidden, ex.Message, null),

                UnauthorizedAccessException ex => (
                    StatusCodes.Status401Unauthorized, ex.Message, null),

                // Ràng buộc DB (unique index, FK...) — không lộ chi tiết SQL.
                DbUpdateException => (
                    StatusCodes.Status409Conflict,
                    "Dữ liệu vi phạm ràng buộc. Có thể bản ghi đã tồn tại.", null),

                OperationCanceledException => (0, "", null),   // client tự ngắt

                // Lỗi còn lại: KHÔNG lộ message thật, chỉ đưa traceId để tra log.
                _ => (StatusCodes.Status500InternalServerError,
                    $"Đã xảy ra lỗi không mong muốn. Vui lòng gửi mã traceId '{traceId}' cho bộ phận kỹ thuật.",
                    null)
            };

            if (status == 0) return false;

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Lỗi chưa xử lý. TraceId={TraceId}", traceId);
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(message, traceId, errors), ct);

            return true;
        }
    }
}
