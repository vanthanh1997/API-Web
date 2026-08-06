using Application.Common.Interfaces;
using FluentValidation;
namespace Application.Auth.Commands.ResetPassword
{
    public record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand;
    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        }
    }
    public class ResetPasswordCommandHandler(IIdentityService identity)
    : MediatR.IRequestHandler<ResetPasswordCommand>
    {
        public Task Handle(ResetPasswordCommand request, CancellationToken ct)
            => identity.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
    }
}
