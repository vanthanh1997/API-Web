using Application.Auth.Models;
using Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Auth.Commands.Login
{
    public record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;

    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty()
                .WithMessage("Vui lòng nhập Email của bạn")
                .EmailAddress().WithMessage("Định dạng Email không chính xác");
            RuleFor(x => x.Password).NotEmpty();
        }
    }
    public class LoginCommandHandler(IIdentityService identity) : IRequestHandler<LoginCommand, AuthResponse>
    {
        public Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct) => identity.LoginAsync(request.Email, request.Password, ct);

    }
}
