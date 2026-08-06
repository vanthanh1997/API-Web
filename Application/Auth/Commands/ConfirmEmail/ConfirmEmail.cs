using Application.Common.Interfaces;
using FluentValidation;
namespace Application.Auth.Commands.ConfirmEmail
{
    public record ConfirmEmailCommand(string UserId, string Token) : ICommand;

    public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
    {
        public ConfirmEmailCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Token).NotEmpty();
        }
    }

    public class ConfirmEmailCommandHandler(IIdentityService identity)
        : MediatR.IRequestHandler<ConfirmEmailCommand>
    {
        public Task Handle(ConfirmEmailCommand request, CancellationToken ct)
            => identity.ConfirmEmailAsync(request.UserId, request.Token, ct);
    }
}
