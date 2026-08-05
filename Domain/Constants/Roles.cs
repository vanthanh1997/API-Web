namespace Domain.Constants
{
    public static class Roles
    {
        public const string Admin = nameof(Admin);
        public const string Manager = nameof(Manager);
        public const string Customer = nameof(Customer);
        public static readonly string[] All = [Admin, Manager, Customer];

        /// <summary>
        /// Các role được phép vào khu vực quản trị (cửa /auth/admin/login).
        /// Customer KHÔNG nằm ở đây nên không lấy được token quản trị.
        /// </summary>
        public static readonly string[] BackOffice = [Admin, Manager];
    }
}
