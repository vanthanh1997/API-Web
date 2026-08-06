using Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Auth.Commands.Register
{
    /// <summary>
    /// Đăng ký tài khoản phía shop. Trả về Id của user vừa tạo; email xác nhận
    /// được đẩy vào hàng đợi Hangfire nên request không phải chờ SMTP.
    /// </summary>
    public record RegisterCommand(string Email, string Password) : ICommand<string>;

    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty()
                .WithMessage("Vui lòng nhập Email của bạn")
                .EmailAddress().WithMessage("Định dạng Email không chính xác")
                .MaximumLength(256);

            // MinimumLength (không phải MaximumLength) — phải khớp
            // options.Password.RequiredLength = 8 khai trong Infrastructure/DependencyInjection.cs.
            // Đặt ngược thành MaximumLength(8) sẽ chặn gần như mọi mật khẩu hợp lệ,
            // mà lỗi lại hiện ra ở tầng Identity nên rất khó truy.
            RuleFor(x => x.Password).NotEmpty()
                .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự")
                // Chặn trên để không ai gửi mật khẩu 10 MB làm BCrypt/PBKDF2 treo CPU.
                .MaximumLength(128).WithMessage("Mật khẩu không được vượt quá 128 ký tự");
        }
    }
    public class RegisterCommandHandler(IIdentityService identity)
      : IRequestHandler<RegisterCommand, string>
    {
        public Task<string> Handle(RegisterCommand request, CancellationToken ct)
            => identity.RegisterAsync(request.Email, request.Password, ct);
    }
}
