using Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Users.Commands
{
    /// <summary>Khoá tài khoản. Until = null nghĩa là khoá vĩnh viễn.</summary>
    public record LockUserCommand(string UserId, DateTimeOffset? Until = null) : ICommand;

    public class LockUserCommandValidator : AbstractValidator<LockUserCommand>
    {
        public LockUserCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class LockUserCommandHandler(IUserAdminService users)
        : IRequestHandler<LockUserCommand>
    {
        public Task Handle(LockUserCommand request, CancellationToken ct)
            => users.LockUserAsync(request.UserId, request.Until, ct);
    }
}
