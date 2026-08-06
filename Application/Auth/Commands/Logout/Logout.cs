using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Commands.Logout
{
    public record LogoutCommand(string RefreshToken) : ICommand;

    public class LogoutCommandHandler(IIdentityService identity)
    : IRequestHandler<LogoutCommand>
    {
        public Task Handle(LogoutCommand request, CancellationToken ct)
            => identity.LogoutAsync(request.RefreshToken, ct);
    }
}
