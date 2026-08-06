using Application.Common.Interfaces;
using Domain.Constants;
using FluentValidation;
using MediatR;

namespace Application.Users.Commands
{
    /// <summary>
    /// Đặt lại toàn bộ role của user. Truyền danh sách nào thì user có đúng danh sách đó.
    /// </summary>
    public record UpdateUserRolesCommand(string UserId, IReadOnlyList<string> Roles) : ICommand;

    public class UpdateUserRolesCommandValidator : AbstractValidator<UpdateUserRolesCommand>
    {
        public UpdateUserRolesCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();

            RuleFor(x => x.Roles).NotNull();

            // Chặn role lạ ngay từ đầu: gán role không tồn tại sẽ tạo ra user
            // không thuộc nhóm nào mà nhìn vào DB vẫn tưởng đã phân quyền.
            RuleForEach(x => x.Roles)
                .Must(Roles.All.Contains)
                .WithMessage($"Role không hợp lệ. Chỉ chấp nhận: {string.Join(", ", Roles.All)}.");
        }
    }

    public class UpdateUserRolesCommandHandler(IUserAdminService users)
        : IRequestHandler<UpdateUserRolesCommand>
    {
        public Task Handle(UpdateUserRolesCommand request, CancellationToken ct)
            => users.UpdateRolesAsync(request.UserId, request.Roles, ct);
    }
}
