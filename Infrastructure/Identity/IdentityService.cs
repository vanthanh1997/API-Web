using Application.Auth.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Constants;
using Google.Apis.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.Server;

namespace Infrastructure.Identity
{
    public class IdentityService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IJwtTokenService jwt,
    IEmailService emailService,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GoogleAuthOptions> googleOptions,
    TimeProvider clock) : IIdentityService
    {
        private readonly JwtOptions _jwt = jwtOptions.Value;
        private readonly GoogleAuthOptions _google = googleOptions.Value;

        private const string GoogleProvider = "Google";

        public async Task ConfirmEmailAsync(string userId, string token, CancellationToken ct)
        {
            var user = await userManager.FindByIdAsync(userId);
            NotFoundException.ThrowIfNull(user, "User", userId);
            // 1. Tối ưu UX: Nếu Email đã được xác thực trước đó rồi thì bỏ qua, coi như thành công luôn
            if (user!.EmailConfirmed)
            {
                return;
            }
            var result = await userManager.ConfirmEmailAsync(user!, IdentityTokenEncoder.Decode(token));

            if (!result.Succeeded)
            {
                throw new ConflictException("Liên kết xác nhận không hợp lệ hoặc đã hết hạn.");
            }
        }

        public async Task ForgotPasswordAsync(string email, CancellationToken ct)
        {
            var user = await userManager.FindByEmailAsync(email);

            // Email không tồn tại vẫn trả về như thành công — tránh để kẻ xấu dò
            // email nào đã đăng ký (user enumeration).
            if (user is null) return;

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            emailService.QueueResetPassword(email, token);
        }

        public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct)
        {
            var user = await AuthenticateAsync(email, password, ct);

            return await IssueTokensAsync(user, ct);
        }

        /// <summary>
        /// Xác thực email + mật khẩu và trả về user, hoặc throw nếu không hợp lệ.
        /// Tách riêng để cửa shop (LoginAsync) và cửa quản trị (AdminLoginAsync) dùng
        /// CHUNG một bộ kiểm tra — sửa quy tắc khoá tài khoản chỉ phải sửa ở đây,
        /// không sợ hai cửa lệch nhau.
        /// </summary>
        private async Task<ApplicationUser> AuthenticateAsync(
            string email, string password, CancellationToken ct)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                // Không nói rõ sai email hay sai mật khẩu — tránh lộ email nào đã đăng ký.
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
            }

            // Kiểm tra khoá TRƯỚC khi thử mật khẩu — đang bị khoá thì không cho thử tiếp.
            if (await userManager.IsLockedOutAsync(user))
            {
                throw new ForbiddenAccessException("Tài khoản đang bị khoá tạm thời do đăng nhập sai nhiều lần.");
            }

            if (!await userManager.CheckPasswordAsync(user, password))
            {
                // BẮT BUỘC gọi AccessFailedAsync: CheckPasswordAsync KHÔNG tự đếm số lần sai,
                // thiếu dòng này thì MaxFailedAccessAttempts=5 vô tác dụng và kẻ xấu
                // dò mật khẩu được vô hạn.
                await userManager.AccessFailedAsync(user);

                throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
            }

            // Đúng mật khẩu -> xoá bộ đếm, tránh cộng dồn lỗi từ những lần trước đó.
            await userManager.ResetAccessFailedCountAsync(user);

            return user;
        }

        public async Task<AuthResponse> AdminLoginAsync(string email, string password, CancellationToken ct)
        {
            // Dùng lại toàn bộ kiểm tra của LoginAsync (khoá tài khoản, đếm số lần sai,
            // chống dò email) nhưng KIỂM TRA ROLE TRƯỚC KHI CẤP TOKEN.
            //
            // Không gọi LoginAsync rồi thu hồi token sau: cách đó cấp token xong mới huỷ,
            // vừa ghi thêm một dòng vô ích vào RefreshTokens, vừa phụ thuộc vào việc
            // transaction có rollback đúng hay không (AdminLoginCommand là ICommand nên
            // TransactionBehaviour bọc transaction — throw sẽ rollback cả lệnh thu hồi,
            // khiến dòng ExecuteUpdateAsync đó thành vô nghĩa).
            var user = await AuthenticateAsync(email, password, ct);

            var roles = await userManager.GetRolesAsync(user);

            // Đúng mật khẩu nhưng không thuộc role quản trị => KHÔNG cấp token ở cửa này.
            if (!roles.Any(Roles.BackOffice.Contains))
            {
                throw new ForbiddenAccessException("Tài khoản không có quyền truy cập khu vực quản trị.");
            }

            return await IssueTokensAsync(user, ct);
        }

        public async Task<AuthResponse> GoogleLoginAsync(string idToken, CancellationToken ct)
        {
            // appsettings.json ship ClientId = "" có chủ đích (không commit secret).
            // Chưa điền mà vẫn cho chạy thì Audience = [""] và lỗi hiện ra dưới dạng
            // "token không hợp lệ" — người dùng tưởng token sai trong khi thật ra là
            // thiếu cấu hình. Chặn ngay đây với thông báo chỉ rõ phải sửa ở đâu.
            if (string.IsNullOrWhiteSpace(_google.ClientId))
            {
                throw new InvalidOperationException(
                    "Chưa cấu hình GoogleAuth:ClientId — không thể đăng nhập bằng Google. " +
                    "Lấy OAuth 2.0 Client ID từ Google Cloud Console rồi đặt vào appsettings " +
                    "(hoặc user-secrets / biến môi trường ở production).");
            }

            GoogleJsonWebSignature.Payload payload;

            try
            {
                // Bắt buộc khai Audience: không có thì idToken do BẤT KỲ app Google nào
                // cấp cũng qua được, kẻ xấu tự tạo app riêng là đăng nhập được vào đây.
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [_google.ClientId]
                    });
            }
            catch (InvalidJwtException)
            {
                throw new UnauthorizedAccessException("Token Google không hợp lệ hoặc đã hết hạn.");
            }

            // Google có thể trả tài khoản chưa xác minh email (hiếm, nhưng có).
            // Nhận vào là mở đường chiếm tài khoản: kẻ xấu tạo tài khoản Google với
            // email của người khác rồi login chiếm luôn tài khoản shop của họ.
            if (!payload.EmailVerified)
            {
                throw new ForbiddenAccessException("Email Google chưa được xác minh.");
            }

            var user = await userManager.FindByEmailAsync(payload.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FullName = payload.Name,
                    // Google đã xác minh email hộ rồi, không cần gửi mail xác nhận nữa.
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user);

                if (!result.Succeeded)
                {
                    throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
                }

                // Google login chỉ dành cho phía shop nên luôn là Customer.
                await userManager.AddToRoleAsync(user, Roles.Customer);
            }

            // Liên kết Google vào ĐÚNG tài khoản đã có thay vì tạo tài khoản thứ hai —
            // khách từng đăng ký bằng mật khẩu rồi chuyển sang login Google vẫn giữ
            // nguyên đơn hàng và lịch sử mua.
            var logins = await userManager.GetLoginsAsync(user);

            if (!logins.Any(l => l.LoginProvider == GoogleProvider && l.ProviderKey == payload.Subject))
            {
                await userManager.AddLoginAsync(user,
                    new UserLoginInfo(GoogleProvider, payload.Subject, GoogleProvider));
            }

            // Tài khoản bị khoá thì login kiểu nào cũng phải chặn, kể cả qua Google.
            if (await userManager.IsLockedOutAsync(user))
            {
                throw new ForbiddenAccessException("Tài khoản đang bị khoá.");
            }

            return await IssueTokensAsync(user, ct);
        }

        public async Task LogoutAsync(string refreshToken, CancellationToken ct)
        {

            var entry = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

            if (entry is not null)
            {
                entry.RevokedAt = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        {

            var now = clock.GetUtcNow();

            var entry = await db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

            if (entry is null || !entry.IsActive(now))
            {
                throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");
            }

            // Rotation: thu hồi token cũ rồi cấp cặp mới. Cả hai nằm chung transaction
            // do TransactionBehaviour mở, nên không thể revoke xong mà cấp mới lỗi.
            entry.RevokedAt = now;
            await db.SaveChangesAsync(ct);

            return await IssueTokensAsync(entry.User, ct);
        }

        public async Task<string> RegisterAsync(string email, string password, CancellationToken ct)
        {
            var user = new ApplicationUser { UserName = email, Email = email };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, Roles.Customer);

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            emailService.QueueConfirmEmail(email, user.Id, token);

            return user.Id;
        }

        public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                throw new ConflictException("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
            }
            var result = await userManager.ResetPasswordAsync(user, IdentityTokenEncoder.Decode(token), newPassword);
            if (!result.Succeeded)
            {
                throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            // Đổi mật khẩu xong thì thu hồi mọi refresh token cũ — thiết bị khác phải đăng nhập lại.
            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);
        }
        private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var roles = await userManager.GetRolesAsync(user);

            // Dọn rác trước khi cấp mới: xoá cứng token đã CHẾT của chính user này.
            // Không có bước này thì mỗi lần login/refresh lại thêm một dòng mà không
            // bao giờ bớt — bảng RefreshTokens phình vô hạn, và token cũ nằm lại lâu
            // dài làm tăng bề mặt rủi ro nếu DB bị lộ.
            // Hai điều kiện cố ý:
            //   - Chỉ xoá token chết (hết hạn / đã thu hồi) — token còn sống của thiết
            //     bị khác giữ nguyên, user vẫn đăng nhập song song nhiều thiết bị.
            //   - Token ĐÃ THU HỒI chỉ xoá sau 7 ngày ân hạn — giữ lại dấu vết để còn
            //     phân biệt "token bị dùng lại sau rotation" (dấu hiệu bị đánh cắp)
            //     với "token chưa từng tồn tại" khi điều tra/log.
            var revokedCutoff = now.AddDays(-7);
            await db.RefreshTokens
                .Where(t => t.UserId == user.Id
                    && (t.ExpiresAt <= now || t.RevokedAt <= revokedCutoff))
                .ExecuteDeleteAsync(ct);

            var accessToken = jwt.CreateAccessToken(user.Id, user.Email!, roles);
            var refreshToken = jwt.CreateRefreshToken();

            db.RefreshTokens.Add(new RefreshTokenEntry
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = now.AddDays(_jwt.RefreshTokenDays)
            });

            await db.SaveChangesAsync(ct);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = now.AddMinutes(_jwt.AccessTokenMinutes),
                Roles = [.. roles]
            };
        }
    }
}
