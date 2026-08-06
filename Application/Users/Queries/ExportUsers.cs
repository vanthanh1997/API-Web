using Application.Common.Exporting;
using Application.Common.Interfaces;
using Application.Users.Models;
using Domain.Constants;
using FluentValidation;
using MediatR;
namespace Application.Users.Queries
{
    /// <summary>
    /// Chuẩn bị dữ liệu để xuất danh sách user ra file.
    ///
    /// Handler KHÔNG tự ghi file và KHÔNG trả byte[]: nó trả về một
    /// <see cref="ExportSource{T}"/> gồm (dòng dạng stream + tổng số dòng + cách hiển thị).
    /// Việc ghi ra HTTP là chuyện của Presentation (ExportEndpoint) — nhờ tách vậy,
    /// cùng một query này dùng được cho cả tải trực tiếp và job Hangfire ghi ra đĩa.
    /// </summary>
    public record ExportUsersQuery(
        string? Keyword = null,
        string? Role = null) : IRequest<ExportSource<UserDto>>;

    public class ExportUsersQueryValidator : AbstractValidator<ExportUsersQuery>
    {
        public ExportUsersQueryValidator()
        {
            // Lọc theo role thì phải là role có thật, nếu không người dùng nhận về
            // file rỗng mà tưởng là hệ thống mất dữ liệu.
            RuleFor(x => x.Role)
                .Must(r => string.IsNullOrWhiteSpace(r) || Roles.All.Contains(r))
                .WithMessage($"Role không hợp lệ. Chỉ chấp nhận: {string.Join(", ", Roles.All)}.");
        }
    }

    public class ExportUsersQueryHandler(IUserAdminService users)
        : IRequestHandler<ExportUsersQuery, ExportSource<UserDto>>
    {
        public async Task<ExportSource<UserDto>> Handle(ExportUsersQuery request, CancellationToken ct)
        {
            // Đếm trước để biết có bao nhiêu dòng: ExportEndpoint cần con số này mới
            // quyết định được giữ nguyên định dạng hay đổi sang loại chịu được dữ liệu lớn.
            var count = await users.CountUsersAsync(request.Keyword, request.Role, ct);

            var definition = new ExportDefinition<UserDto>
            {
                Title = "Danh sách người dùng",
                FileName = "danh-sach-nguoi-dung"
            };

            definition
                .Column("Email", u => u.Email, width: 32)
                .Column("Họ và tên", u => u.FullName, width: 28)
                .Column("Vai trò", u => string.Join(", ", u.Roles), width: 24)
                .Column("Đã xác nhận email", u => u.EmailConfirmed, width: 18, ExportAlign.Center)
                .Column("Đang bị khoá", u => u.IsLockedOut, width: 14, ExportAlign.Center)
                // Truyền DateTime thật (không ToString) để ô Excel là ô NGÀY —
                // người dùng còn sort/filter theo ngày được.
                .Column("Khoá đến", u => u.LockoutEnd?.DateTime, width: 18,
                    ExportAlign.Center, format: "dd/MM/yyyy HH:mm");

            // Rows là stream — tới đây CHƯA có dòng nào được đọc khỏi DB.
            // Writer duyệt tới đâu, EF đọc tới đó.
            return new ExportSource<UserDto>(
                users.StreamUsersAsync(request.Keyword, request.Role, ct),
                count,
                definition);
        }
    }
}
