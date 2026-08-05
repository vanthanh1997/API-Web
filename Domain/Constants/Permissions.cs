using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Constants
{
    public static class Permissions
    {
        public const string ClaimType = "permission";

        public const string ProductsRead = "products.read";
        public const string ProductsCreate = "products.create";
        public const string ProductsUpdate = "products.update";
        public const string ProductsDelete = "products.delete";
        public const string UsersRead = "users.read";
        public const string UsersManage = "users.manage";

        public static readonly Dictionary<string, string[]> ByRole = new()
        {
            [Roles.Admin] =
        [
            ProductsRead, ProductsCreate, ProductsUpdate, ProductsDelete,
            UsersRead, UsersManage
        ],
            [Roles.Manager] =
        [
            ProductsRead, ProductsCreate, ProductsUpdate,
            UsersRead
        ],
            [Roles.Customer] =
        [
            ProductsRead
        ]
        };
    }
}
