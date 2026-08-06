namespace Infrastructure.Identity
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = null!;

        public string Audience { get; set; } = null!;

        public string Secret { get; set; } = null!;

        public int AccessTokenMinutes { get; set; } = 15;

        public int RefreshTokenDays { get; set; } = 7;
    }
}
