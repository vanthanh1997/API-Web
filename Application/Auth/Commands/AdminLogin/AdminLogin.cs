using Application.Auth.Models;
using Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Auth.Commands.AdminLogin
{
    /// <summary>
    /// Đăng nhập khu vực quản trị. Tách khỏi LoginCommand để khách hàng (Customer)
    /// không thể lấy token qua cửa này, dù mật khẩu đúng.
    /// </summary>
    public record AdminLoginCommand(string Email, string Password) : ICommand<AuthResponse>;

    public class AdminLoginCommandValidator : AbstractValidator<AdminLoginCommand>
    {
        public AdminLoginCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty()
                .WithMessage("Vui lòng nhập Email của bạn")
                .EmailAddress().WithMessage("Định dạng Email không chính xác");

            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class AdminLoginCommandHandler(IIdentityService identity)
        : IRequestHandler<AdminLoginCommand, AuthResponse>
    {
        public Task<AuthResponse> Handle(AdminLoginCommand request, CancellationToken ct)
            => identity.AdminLoginAsync(request.Email, request.Password, ct);
    }
}
