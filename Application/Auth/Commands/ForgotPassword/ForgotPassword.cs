using Application.Common.Interfaces;
using FluentValidation;

namespace Application.Auth.Commands.ForgotPassword
{
    public record ForgotPasswordCommand(string Email) : ICommand;

    public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ForgotPasswordCommandHandler(IIdentityService identity)
        : MediatR.IRequestHandler<ForgotPasswordCommand>
    {
        public Task Handle(ForgotPasswordCommand request, CancellationToken ct)
            => identity.ForgotPasswordAsync(request.Email, ct);
    }
}
