using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Presentation.Infrastructure.Authorization
{
    public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
     : DefaultAuthorizationPolicyProvider(options)
    {
        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            {
                return await base.GetPolicyAsync(policyName);
            }

            var permission = policyName[PermissionPolicy.Prefix.Length..];

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(Permissions.ClaimType, permission)
                .Build();
        }
    }

    public static class PermissionPolicy
    {
        public const string Prefix = "perm:";
    }
}
