using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Presentation.Infrastructure
{
    public class ApiResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var payload = UnwrapPayloadType(context.MethodInfo.ReturnType);
            var successSchema = SchemaFor(typeof(ApiResponse<>).MakeGenericType(payload), context);

            var responses = new OpenApiResponses();

            foreach (var (statusCode, response) in operation.Responses)
            {
                // Mọi mã 2xx đều bị bọc lúc chạy — kể cả 204 (đổi thành 200 + body)
                // và các action khai IActionResult (Swashbuckle không đoán được kiểu).
                if (statusCode.StartsWith('2'))
                {
                    var code = statusCode == "204" ? "200" : statusCode;

                    responses[code] = new OpenApiResponse
                    {
                        Description = response.Description,
                        Content = successSchema
                    };

                    continue;
                }

                responses[statusCode] = response;
            }

            // POST tạo mới trả 201 + header Location, nhưng Swashbuckle mặc định khai 200
            // nếu action không có [ProducesResponseType]. Sửa lại cho khớp thực tế.
            if (IsCreatedAction(context) && responses.Remove("200", out var created))
            {
                created.Description = "Đã tạo";
                created.Headers["Location"] = new OpenApiHeader
                {
                    Description = "Đường dẫn tới tài nguyên vừa tạo.",
                    Schema = new OpenApiSchema { Type = "string" }
                };

                responses["201"] = created;
            }

            operation.Responses = responses;

            AddError(operation, context, "400", "Dữ liệu không hợp lệ");
            AddError(operation, context, "500", "Lỗi hệ thống");
        }

        private static bool IsCreatedAction(OperationFilterContext context)
            => context.ApiDescription.HttpMethod == "POST"
               && !context.MethodInfo.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), true).Any();

        private static void AddError(
            OpenApiOperation operation, OperationFilterContext context, string code, string description)
        {
            if (operation.Responses.ContainsKey(code)) return;

            operation.Responses[code] = new OpenApiResponse
            {
                Description = description,
                Content = SchemaFor(typeof(ApiResponse<object>), context)
            };
        }

        private static Dictionary<string, OpenApiMediaType> SchemaFor(
            Type type, OperationFilterContext context) => new()
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository)
                }
            };

        /// <summary>
        /// Bóc Task&lt;ActionResult&lt;ProductDto&gt;&gt; → ProductDto.
        /// Action khai IActionResult/Task không mang kiểu cụ thể → dùng object.
        /// </summary>
        private static Type UnwrapPayloadType(Type type)
        {
            if (type == typeof(Task) || type == typeof(void)) return typeof(object);

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
            {
                type = type.GetGenericArguments()[0];
            }

            return typeof(IActionResult).IsAssignableFrom(type) ? typeof(object) : type;
        }
    }
}
