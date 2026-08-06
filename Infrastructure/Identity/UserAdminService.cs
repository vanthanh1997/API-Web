using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Users.Models;
using Domain.Constants;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity
{
    public class UserAdminService(
       UserManager<ApplicationUser> userManager,
       AppDbContext db,
       ICurrentUser currentUser,
       ITokenBlacklist tokenBlacklist,
       TimeProvider clock) : IUserAdminService
    {
        /// <summary>
        /// Dựng câu truy vấn user theo điều kiện lọc. Tách riêng để GetUsersAsync (phân trang),
        /// CountUsersAsync và StreamUsersAsync (export) dùng CHUNG một bộ lọc — nếu không,
        /// sửa cách lọc ở một chỗ sẽ làm số liệu trên lưới và trong file export lệch nhau.
        /// </summary>
        private async Task<IQueryable<ApplicationUser>> FilterUsersAsync(
            string? keyword, string? role, CancellationToken ct)
        {
            var query = userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(u => u.Email!.Contains(term) || u.FullName!.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                // Join qua bảng Identity thay vì GetUsersInRoleAsync — cách kia nạp
                // toàn bộ user của role vào RAM rồi mới lọc, hỏng khi role có nhiều user.
                var roleId = await db.Roles
                    .Where(r => r.Name == role)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync(ct);

                var userIds = db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId);
                query = query.Where(u => userIds.Contains(u.Id));
            }

            return query;
        }

        public async Task<int> CountUsersAsync(string? keyword, string? role, CancellationToken ct)
        {
            var query = await FilterUsersAsync(keyword, role, ct);

            return await query.CountAsync(ct);
        }

        public async IAsyncEnumerable<UserDto> StreamUsersAsync(
            string? keyword,
            string? role,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var query = await FilterUsersAsync(keyword, role, ct);

            // Role của từng user lấy bằng MỘT query cho cả bảng rồi tra trong bộ nhớ.
            // Không gọi GetRolesAsync trong vòng lặp: export 100.000 user sẽ thành
            // 100.000 lượt truy DB.
            // Bảng UserRoles chỉ chứa cặp (UserId, RoleId) nên rất nhẹ so với bảng user.
            var rolesByUser = await (from ur in db.UserRoles
                                     join r in db.Roles on ur.RoleId equals r.Id
                                     select new { ur.UserId, r.Name })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name!).ToList(), ct);

            var now = clock.GetUtcNow();

            // AsAsyncEnumerable: EF đọc theo từng dòng từ DataReader, KHÔNG ToListAsync
            // cả bảng vào RAM — đây là điểm khiến export chịu được cỡ trăm nghìn dòng.
            await foreach (var u in query.OrderBy(u => u.Email).AsAsyncEnumerable().WithCancellation(ct))
            {
                yield return new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    EmailConfirmed = u.EmailConfirmed,
                    IsLockedOut = u.LockoutEnd > now,
                    LockoutEnd = u.LockoutEnd,
                    Roles = rolesByUser.TryGetValue(u.Id, out var roles) ? roles : []
                };
            }
        }

        public async Task<PagedList<UserDto>> GetUsersAsync(
            string? keyword, string? role, int page, int pageSize, CancellationToken ct)
        {
            var query = await FilterUsersAsync(keyword, role, ct);

            var paged = await PagedList<ApplicationUser>.CreateAsync(
                query.OrderBy(u => u.Email), page, pageSize, ct);

            // Lấy role của cả trang bằng MỘT query thay vì gọi GetRolesAsync cho từng
            // user (N+1: 20 user = 20 lượt truy DB).
            var ids = paged.Items.Select(u => u.Id).ToList();

            var rolesByUser = await (from ur in db.UserRoles
                                     join r in db.Roles on ur.RoleId equals r.Id
                                     where ids.Contains(ur.UserId)
                                     select new { ur.UserId, r.Name })
                .ToListAsync(ct);

            var now = clock.GetUtcNow();

            var items = paged.Items.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                EmailConfirmed = u.EmailConfirmed,
                IsLockedOut = u.LockoutEnd > now,
                LockoutEnd = u.LockoutEnd,
                Roles = rolesByUser.Where(x => x.UserId == u.Id).Select(x => x.Name!).ToList()
            }).ToList();

            return new PagedList<UserDto>
            {
                Items = items,
                Page = paged.Page,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount
            };
        }

        public async Task<UserDto> GetUserByIdAsync(string userId, CancellationToken ct)
        {
            var user = await userManager.FindByIdAsync(userId);
            NotFoundException.ThrowIfNull(user, "User", userId);

            var roles = await userManager.GetRolesAsync(user!);
            var now = clock.GetUtcNow();

            return new UserDto
            {
                Id = user!.Id,
                Email = user.Email,
                FullName = user.FullName,
                EmailConfirmed = user.EmailConfirmed,
                IsLockedOut = user.LockoutEnd > now,
                LockoutEnd = user.LockoutEnd,
                Roles = [.. roles]
            };
        }

        public async Task UpdateRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct)
        {
            var user = await userManager.FindByIdAsync(userId);
            NotFoundException.ThrowIfNull(user, "User", userId);

            // Admin tự gỡ quyền Admin của chính mình là mất đường vào trang quản trị,
            // phải sửa tay dưới DB mới khôi phục được.
            if (userId == currentUser.Id && !roles.Contains(Roles.Admin))
            {
                throw new ConflictException("Không thể tự gỡ quyền Admin của chính mình.");
            }

            var current = await userManager.GetRolesAsync(user!);

            var toRemove = current.Except(roles).ToList();
            var toAdd = roles.Except(current).ToList();

            if (toRemove.Count > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(user!, toRemove);
                ThrowIfFailed(removed);
            }

            if (toAdd.Count > 0)
            {
                var added = await userManager.AddToRolesAsync(user!, toAdd);
                ThrowIfFailed(added);
            }

            // Permission nằm sẵn trong access token nên đổi role chưa có hiệu lực ngay.
            // Thu hồi refresh token để user buộc đăng nhập lại và nhận quyền mới,
            // thay vì giữ quyền cũ tới khi token hết hạn.
            await RevokeAllAccessAsync(userId, ct);
        }

        public async Task LockUserAsync(string userId, DateTimeOffset? until, CancellationToken ct)
        {
            var user = await userManager.FindByIdAsync(userId);
            NotFoundException.ThrowIfNull(user, "User", userId);

            if (userId == currentUser.Id)
            {
                throw new ConflictException("Không thể tự khoá tài khoản của chính mình.");
            }

            // LockoutEnabled phải bật trước, nếu không SetLockoutEndDateAsync ghi giá trị
            // nhưng Identity vẫn coi tài khoản là mở khoá.
            await userManager.SetLockoutEnabledAsync(user!, true);

            // null = khoá vĩnh viễn (đặt mốc rất xa thay vì null, vì null nghĩa là "chưa từng khoá").
            var lockoutEnd = until ?? DateTimeOffset.MaxValue;

            ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user!, lockoutEnd));

            // Khoá mà không thu hồi refresh token thì user vẫn đổi được token mới
            // và tiếp tục dùng hệ thống như chưa hề bị khoá.
            await RevokeAllAccessAsync(userId, ct);
        }

        public async Task UnlockUserAsync(string userId, CancellationToken ct)
        {
            var user = await userManager.FindByIdAsync(userId);
            NotFoundException.ThrowIfNull(user, "User", userId);

            ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user!, null));

            // Xoá luôn bộ đếm đăng nhập sai, nếu không user vừa mở khoá chỉ cần sai
            // thêm một lần là bị khoá lại ngay.
            await userManager.ResetAccessFailedCountAsync(user!);
        }

        /// <summary>
        /// Thu hồi quyền truy cập của user, gồm CẢ HAI loại token:
        ///
        ///   1. <b>Refresh token</b> (trong DB) — chặn user đổi lấy access token mới.
        ///   2. <b>Access token</b> (đã phát, không nằm trong DB) — chặn qua danh sách đen
        ///      trên Redis, có hiệu lực TỨC THÌ.
        ///
        /// Thiếu bước 2 thì user bị khoá vẫn dùng được hệ thống tối đa 15 phút (tuổi thọ
        /// access token) — quá lâu với tài khoản gian lận. Thiếu bước 1 thì user chỉ cần
        /// gọi /refresh là có token mới, coi như chưa từng bị khoá.
        /// </summary>
        private async Task RevokeAllAccessAsync(string userId, CancellationToken ct)
        {
            var now = clock.GetUtcNow();

            await db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

            await tokenBlacklist.RevokeAllForUserAsync(userId, now, ct);
        }

        private static void ThrowIfFailed(IdentityResult result)
        {
            if (!result.Succeeded)
            {
                throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
