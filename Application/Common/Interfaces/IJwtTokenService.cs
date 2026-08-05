namespace Application.Common.Interfaces
{
    public interface IJwtTokenService
    {
        string CreateAccessToken(string userId, string email, IEnumerable<string> roles);

        string CreateRefreshToken();
    }
}
