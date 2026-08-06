using Microsoft.AspNetCore.Authorization;

namespace Presentation.Infrastructure.Authorization
{
    /// <summary>
    /// Yêu cầu user có permission cụ thể. Dùng: [HasPermission(Permissions.ProductsCreate)]
    /// </summary>
    public class HasPermissionAttribute(string permission)
        : AuthorizeAttribute(PermissionPolicy.Prefix + permission);
}
