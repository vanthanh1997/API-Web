using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Models
{
    public class CursorPage<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = [];

        /// <summary>Con trỏ để lấy trang kế. null = hết dữ liệu.</summary>
        public string? NextCursor { get; init; }

        public bool HasMore => NextCursor is not null;
    }
}
