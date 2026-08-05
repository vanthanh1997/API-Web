using Application.Common.Models;
using Application.Users.Models;
namespace Application.Common.Interfaces
{
    /// <summary>
    /// Nghiệp vụ quản trị user. Tách khỏi IIdentityService (lo đăng nhập/đăng ký)
    /// để phần dành cho admin không lẫn với phần công khai của shop.
    /// </summary>
    public interface IUserAdminService
    {
        Task<PagedList<UserDto>> GetUsersAsync(
            string? keyword, string? role, int page, int pageSize, CancellationToken ct);

        Task<UserDto> GetUserByIdAsync(string userId, CancellationToken ct);

        /// <summary>
        /// Số user khớp điều kiện lọc. Dùng để biết trước có bao nhiêu dòng mà chọn
        /// định dạng export phù hợp (xem ExportFormatInfo.DirectExportLimit).
        /// </summary>
        Task<int> CountUsersAsync(string? keyword, string? role, CancellationToken ct);

        /// <summary>
        /// Trả user theo kiểu STREAM cho việc export: đọc tới đâu ghi ra file tới đó,
        /// KHÔNG nạp cả bảng vào RAM như GetUsersAsync.
        /// </summary>
        IAsyncEnumerable<UserDto> StreamUsersAsync(
            string? keyword, string? role, CancellationToken ct);

        /// <summary>Đặt lại toàn bộ role của user thành đúng danh sách truyền vào.</summary>
        Task UpdateRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct);

        /// <summary>Khoá tài khoản tới thời điểm chỉ định (null = khoá vĩnh viễn).</summary>
        Task LockUserAsync(string userId, DateTimeOffset? until, CancellationToken ct);

        Task UnlockUserAsync(string userId, CancellationToken ct);
    }
}
