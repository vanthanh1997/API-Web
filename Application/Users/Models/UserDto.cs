namespace Application.Users.Models
{
    /// <summary>Thông tin user hiển thị trong trang quản trị.</summary>
    public class UserDto
    {
        public string Id { get; init; } = null!;

        public string? Email { get; init; }

        public string? FullName { get; init; }

        public bool EmailConfirmed { get; init; }

        /// <summary>Đang bị khoá hay không (tính theo LockoutEnd so với hiện tại).</summary>
        public bool IsLockedOut { get; init; }

        public DateTimeOffset? LockoutEnd { get; init; }

        public IReadOnlyList<string> Roles { get; init; } = [];
    }
}
