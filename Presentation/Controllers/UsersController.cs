using Application.Common.Exporting;
using Application.Common.Models;
using Application.Users.Commands;
using Application.Users.Models;
using Application.Users.Queries;
using Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Presentation.Infrastructure.Authorization;
using Presentation.Infrastructure.Exporting;

namespace Presentation.Controllers
{
    /// <summary>
    /// API quản trị người dùng — chỉ dành cho khu vực ADMIN.
    /// Mọi endpoint đều đòi permission, khách hàng (Customer) không có
    /// users.read/users.manage nên không gọi được.
    /// </summary>
    [Route("api/admin/[controller]")]
    [ApiController]
    public class UsersController(ISender sender, IExportService exportService) : ControllerBase
    {
        /// <summary>Danh sách user, lọc theo từ khoá (email/tên) và role.</summary>
        [HasPermission(Permissions.UsersRead)]
        [HttpGet]
        public async Task<ActionResult<PagedList<UserDto>>> GetUsers(
            [FromQuery] GetUsersQuery query, CancellationToken ct)
            => await sender.Send(query, ct);

        [HasPermission(Permissions.UsersRead)]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(string id, CancellationToken ct)
            => await sender.Send(new GetUserByIdQuery(id), ct);

        /// <summary>
        /// Xuất danh sách user ra file (Excel/CSV/PDF/Word/HTML).
        /// Dùng chung bộ lọc với GET /api/admin/users nên file khớp đúng những gì đang xem.
        ///
        /// Dữ liệu vượt ngưỡng của định dạng sẽ tự đổi sang loại chịu được (xem ExportEndpoint):
        /// Excel -> ExcelStream, còn lại -> CSV.
        /// </summary>
        /// <param name="format">Excel, ExcelStream, Csv, Pdf, Word hoặc Html.</param>
        [HasPermission(Permissions.UsersRead)]
        [HttpGet("export")]
        // Response là file nhị phân, KHÔNG được để ApiResponseWrapperFilter bọc vào JSON.
        // Filter chỉ bọc ObjectResult/NoContentResult; ở đây ta ghi thẳng vào Response.Body
        // và trả về EmptyResult nên không bị bọc.
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Export(
            [FromQuery] ExportUsersQuery query,
            [FromQuery] ExportFormat format,
            CancellationToken ct)
        {
            var source = await sender.Send(query, ct);

            await this.WriteFileAsync(
                exportService, format, source.Definition, source.Rows, source.Count, ct);

            // Nội dung đã ghi trực tiếp vào Response.Body, không còn gì để MVC trả thêm.
            return new EmptyResult();
        }

        /// <summary>
        /// Đặt lại toàn bộ role của user. Sau khi đổi, refresh token của user bị thu hồi
        /// nên họ phải đăng nhập lại để nhận quyền mới.
        /// </summary>
        [HasPermission(Permissions.UsersManage)]
        [HttpPut("{id}/roles")]
        public async Task<IActionResult> UpdateRoles(
            string id, UpdateUserRolesRequest request, CancellationToken ct)
        {
            await sender.Send(new UpdateUserRolesCommand(id, request.Roles), ct);

            return NoContent();
        }

        /// <summary>Khoá tài khoản. Không truyền until = khoá vĩnh viễn.</summary>
        [HasPermission(Permissions.UsersManage)]
        [HttpPost("{id}/lock")]
        public async Task<IActionResult> Lock(
            string id, LockUserRequest request, CancellationToken ct)
        {
            await sender.Send(new LockUserCommand(id, request.Until), ct);

            return NoContent();
        }

        [HasPermission(Permissions.UsersManage)]
        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> Unlock(string id, CancellationToken ct)
        {
            await sender.Send(new UnlockUserCommand(id), ct);

            return NoContent();
        }

        // Id lấy từ route nên body chỉ chứa phần còn lại — tránh tình trạng
        // id trong URL và id trong body khác nhau.
        public record UpdateUserRolesRequest(IReadOnlyList<string> Roles);

        public record LockUserRequest(DateTimeOffset? Until);
    }
}
