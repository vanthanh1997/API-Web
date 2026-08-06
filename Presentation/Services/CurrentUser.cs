using Application.Common.Interfaces;
using System.Security.Claims;

namespace Presentation.Services
{
    public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal? User => accessor.HttpContext?.User;
        public string? Id => User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public string? Email => User?.FindFirstValue(ClaimTypes.Email)
                                ?? User?.FindFirstValue("email");

        public List<string> Roles =>
            User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

        // Phải ghi đủ namespace: tên property Permissions che mất class Permissions.
        public List<string> Permissions =>
            User?.FindAll(Domain.Constants.Permissions.ClaimType).Select(c => c.Value).ToList() ?? [];
    }
}
