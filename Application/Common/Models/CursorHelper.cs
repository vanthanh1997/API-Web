using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Models
{
    public static class CursorHelper
    {
        /// <summary>
        /// Lấy pageSize+1 dòng để biết còn trang sau hay không mà KHÔNG cần COUNT(*)
        /// — COUNT trên bảng triệu dòng tự nó đã là một lần quét bảng.
        /// </summary>
        public static async Task<CursorPage<TDto>> ToCursorPageAsync<TEntity, TDto>(
            this IQueryable<TDto> query,
            int pageSize,
            Func<TDto, string> cursorSelector,
            CancellationToken ct)
        {
            var items = await query.Take(pageSize + 1).ToListAsync(ct);

            var hasMore = items.Count > pageSize;

            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPage<TDto>
            {
                Items = items,
                NextCursor = hasMore && items.Count > 0
                    ? Encode(cursorSelector(items[^1]))
                    : null
            };
        }

        public static string Encode(string value)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        public static string? Decode(string? cursor)
        {
            if (string.IsNullOrEmpty(cursor)) return null;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            }
            catch (FormatException)
            {
                return null;   // cursor rác → coi như từ đầu, không làm sập request
            }
        }
    }
}
