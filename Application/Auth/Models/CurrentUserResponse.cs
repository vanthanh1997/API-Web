namespace Application.Auth.Models
{
    /// <summary>
    /// Thông tin user đang đăng nhập, đọc từ claims trong token (không truy DB).
    /// Frontend gọi khi mở app để dựng lại UI theo quyền sau khi F5.
    /// </summary>
    public class CurrentUserResponse
    {
        public string Id { get; init; } = null!;

        public string? Email { get; init; }

        public IReadOnlyList<string> Roles { get; init; } = [];

        public IReadOnlyList<string> Permissions { get; init; } = [];
    }
}
