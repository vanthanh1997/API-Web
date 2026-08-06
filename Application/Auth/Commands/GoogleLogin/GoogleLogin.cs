using Application.Auth.Models;
using Application.Common.Interfaces;
using FluentValidation;
using MediatR;
namespace Application.Auth.Commands.GoogleLogin
{
    /// <summary>
    /// Đăng nhập bằng Google cho phía shop.
    /// IdToken do frontend lấy từ Google Sign-In rồi gửi về; server tự verify với Google.
    /// </summary>
    public record GoogleLoginCommand(string IdToken) : ICommand<AuthResponse>;

    public class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
    {
        public GoogleLoginCommandValidator()
        {
            RuleFor(x => x.IdToken).NotEmpty()
                .WithMessage("Thiếu idToken từ Google.");
        }
    }

    public class GoogleLoginCommandHandler(IIdentityService identity)
        : IRequestHandler<GoogleLoginCommand, AuthResponse>
    {
        public Task<AuthResponse> Handle(GoogleLoginCommand request, CancellationToken ct)
            => identity.GoogleLoginAsync(request.IdToken, ct);
    }
}
