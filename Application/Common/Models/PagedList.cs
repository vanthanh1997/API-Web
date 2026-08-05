using Microsoft.EntityFrameworkCore;
namespace Application.Common.Models
{
    public class PagedList<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = [];

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        // Tính TotalPages an toàn, chống lỗi chia cho 0
        public int TotalPages => PageSize > 0
            ? (int)Math.Ceiling(TotalCount / (double)PageSize)
            : 0;

        public bool HasNextPage => Page < TotalPages;

        public bool HasPreviousPage => Page > 1;

        public static async Task<PagedList<T>> CreateAsync(
            IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
        {
            // 1. Sanitize tham số đầu vào (Bảo vệ code không bị crash)
            var pageNumber = page < 1 ? 1 : page;
            var pageSizeNumber = pageSize < 1 ? 10 : pageSize; // Default PageSize = 10 nếu truyền sai

            // 2. Lấy tổng số lượng bản ghi
            var totalCount = await query.CountAsync(ct);

            // 3. Tối ưu: Nếu không có bản ghi nào, trả về ngay lập tức (không query SQL lần 2)
            if (totalCount == 0)
            {
                return new PagedList<T>
                {
                    Items = [],
                    Page = pageNumber,
                    PageSize = pageSizeNumber,
                    TotalCount = 0
                };
            }

            // 4. Phân trang lấy dữ liệu thật
            var items = await query
                .Skip((pageNumber - 1) * pageSizeNumber)
                .Take(pageSizeNumber)
                .ToListAsync(ct);

            return new PagedList<T>
            {
                Items = items,
                Page = pageNumber,
                PageSize = pageSizeNumber,
                TotalCount = totalCount
            };
        }
    }
}
