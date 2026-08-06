using Application.Common.Interfaces;
using Domain.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
namespace Infrastructure.Identity
{
    public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
    {
        private readonly JwtOptions _options = options.Value;
        public string CreateAccessToken(string userId, string email, IEnumerable<string> roles)
        {
            var issuedAt = clock.GetUtcNow();

            var claims = new List<Claim>
            {
               new(JwtRegisteredClaimNames.Sub, userId),
               new(JwtRegisteredClaimNames.Email, email),
               // jti: id duy nhất của token — TokenBlacklistMiddleware dùng để thu hồi
               // đúng một token cụ thể.
               new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
               // iat: thời điểm phát token. Cần cho cơ chế "thu hồi TOÀN BỘ token của
               // user": mọi token có iat <= mốc thu hồi đều bị chặn. Khai tường minh
               // (không dựa vào việc thư viện tự thêm) vì logic bảo mật phụ thuộc vào nó.
               new(JwtRegisteredClaimNames.Iat,
                   issuedAt.ToUnixTimeSeconds().ToString(),
                   ClaimValueTypes.Integer64),
               new(ClaimTypes.NameIdentifier, userId)
            };
            var roleList = roles as string[] ?? roles.ToArray();
            claims.AddRange(roleList.Select(r => new Claim(ClaimTypes.Role, r)));

            // Nhét permission thẳng vào token: authorize không phải truy DB mỗi request.
            // Đánh đổi: đổi quyền của role chỉ có hiệu lực sau khi token hết hạn (15').
            claims.AddRange(roleList
                .SelectMany(r => Permissions.ByRole.TryGetValue(r, out var p) ? p : [])
                .Distinct()
                .Select(p => new Claim(Permissions.ClaimType, p)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
            var now = clock.GetUtcNow();

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
