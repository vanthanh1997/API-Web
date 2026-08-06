using Application.Auth.Models;
using Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Auth.Commands.RefreshToken
{
    public record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResponse>;
    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken).NotEmpty();
        }
    }
    public class RefreshTokenCommandHandler(IIdentityService identity)
    : IRequestHandler<RefreshTokenCommand, AuthResponse>
    {
        public Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
            => identity.RefreshAsync(request.RefreshToken, ct);
    }
}
