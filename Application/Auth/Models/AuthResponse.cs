namespace Application.Auth.Models
{
    public class AuthResponse
    {
        public string AccessToken { get; init; } = null!;

        public string RefreshToken { get; init; } = null!;

        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// Role của user. Trả ra ngoài để frontend render menu/nút theo quyền
        /// mà không phải tự giải mã JWT. Chỉ dùng để HIỂN THỊ —
        /// việc chặn quyền thật vẫn do server làm qua [HasPermission].
        /// </summary>
        public IReadOnlyList<string> Roles { get; init; } = [];
    }
}
