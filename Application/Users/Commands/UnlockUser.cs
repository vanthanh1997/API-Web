using Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Users.Commands
{
    public record UnlockUserCommand(string UserId) : ICommand;

    public class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
    {
        public UnlockUserCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class UnlockUserCommandHandler(IUserAdminService users)
        : IRequestHandler<UnlockUserCommand>
    {
        public Task Handle(UnlockUserCommand request, CancellationToken ct)
            => users.UnlockUserAsync(request.UserId, ct);
    }
}
