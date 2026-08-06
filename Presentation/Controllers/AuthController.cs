using Application.Auth.Commands.AdminLogin;
using Application.Auth.Commands.ConfirmEmail;
using Application.Auth.Commands.GoogleLogin;
using Application.Auth.Commands.Login;
using Application.Auth.Commands.Logout;
using Application.Auth.Commands.RefreshToken;
using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResetPassword;
using Application.Auth.Commands.ForgotPassword;
using Application.Auth.Models;
using Application.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.Infrastructure;

namespace Presentation.Controllers
{
    /// <summary>
    /// ⚠️ KHÔNG đặt [AllowAnonymous] ở cấp class này.
    /// Trong ASP.NET Core, [AllowAnonymous] cấp CONTROLLER THẮNG [Authorize] cấp ACTION,
    /// nên endpoint /me từng cho gọi mà không cần đăng nhập (trả 200 kèm id = null)
    /// dù đã khai [Authorize]. Thay vào đó, đánh [AllowAnonymous] cho TỪNG endpoint
    /// công khai — endpoint nào cần đăng nhập thì mặc định được bảo vệ.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting(SecuritySetup.AuthLimiter)]   // 5 req/phút/IP — chống dò mật khẩu
    public class AuthController(ISender sender) : ControllerBase
    {
        /// <summary>Đăng ký tài khoản phía SHOP. Trả về Id user vừa tạo.</summary>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register(RegisterCommand command, CancellationToken ct)
            => await sender.Send(command, ct);

        /// <summary>Đăng nhập phía SHOP (khách hàng).</summary>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginCommand command, CancellationToken ct)
            => await sender.Send(command, ct);

        /// <summary>
        /// Đăng nhập phía ADMIN. Tài khoản không thuộc role Admin/Manager sẽ bị từ chối
        /// ngay tại đây, không được cấp token — dù mật khẩu đúng.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("admin/login")]
        public async Task<ActionResult<AuthResponse>> AdminLogin(AdminLoginCommand command, CancellationToken ct)
            => await sender.Send(command, ct);

        /// <summary>
        /// Đăng nhập bằng Google — chỉ dành cho phía SHOP, user tạo ra luôn là Customer.
        /// Frontend lấy idToken từ Google Sign-In rồi gửi về đây.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("google")]
        public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginCommand command, CancellationToken ct)
            => await sender.Send(command, ct);

        /// <summary>
        /// Thông tin user đang đăng nhập (id, email, roles, permissions).
        /// Frontend gọi sau khi F5 để dựng lại UI theo quyền.
        /// </summary>
        [Authorize]                              // cần đăng nhập — xem ghi chú ở đầu class
        [DisableRateLimiting]                    // gọi thường xuyên, không nằm trong nhóm chống dò mật khẩu
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
            => await sender.Send(new GetCurrentUserQuery(), ct);

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenCommand command, CancellationToken ct)
            => await sender.Send(command, ct);

        /// <summary>
        /// Đăng xuất — thu hồi refresh token gửi trong body.
        /// Để công khai có chủ đích: client thường gọi logout đúng lúc access token đã
        /// hết hạn, đòi [Authorize] sẽ khiến logout thất bại và token còn sống trong DB.
        /// Không có rủi ro: kẻ xấu chỉ "thu hồi" được token mà chính họ đã có.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
        {
            await sender.Send(command, ct);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailCommand command, CancellationToken ct)
        {
            await sender.Send(command, ct);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken ct)
        {
            await sender.Send(command, ct);

            // Luôn trả 204 dù email có tồn tại hay không — tránh lộ email nào đã đăng ký.
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
        {
            await sender.Send(command, ct);

            return NoContent();
        }

    }
}
