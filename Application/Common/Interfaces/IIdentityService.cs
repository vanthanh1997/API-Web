using Application.Auth.Models;

namespace Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<string> RegisterAsync(string email, string password, CancellationToken ct);

        Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct);

        /// <summary>
        /// Đăng nhập cho khu vực quản trị. Khác LoginAsync ở chỗ: user không thuộc
        /// role quản trị nào sẽ bị TỪ CHỐI ngay, KHÔNG được cấp token — dù mật khẩu đúng.
        /// </summary>
        Task<AuthResponse> AdminLoginAsync(string email, string password, CancellationToken ct);

        /// <summary>
        /// Đăng nhập bằng Google (chỉ dành cho phía shop). Verify idToken với Google,
        /// chưa có tài khoản thì tạo mới với role Customer, có rồi thì liên kết vào
        /// đúng tài khoản đó để không mất lịch sử mua hàng.
        /// </summary>
        Task<AuthResponse> GoogleLoginAsync(string idToken, CancellationToken ct);

        Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct);

        Task LogoutAsync(string refreshToken, CancellationToken ct);

        Task ConfirmEmailAsync(string userId, string token, CancellationToken ct);

        Task ForgotPasswordAsync(string email, CancellationToken ct);

        Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct);
    }
}
