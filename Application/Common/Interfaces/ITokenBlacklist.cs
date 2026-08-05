namespace Application.Common.Interfaces
{
    public interface ITokenBlacklist
    {
        Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken ct);
        Task RevokeAllForUserAsync(string userId, DateTimeOffset until, CancellationToken ct);
        Task<bool> IsRevokedAsync(string jti, string? userId, DateTimeOffset issuedAt, CancellationToken ct);
    }
}
