using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public interface ISoftDeletable
    {
        /// <summary>Đã bị xoá mềm hay chưa. Global Query Filter lọc theo trường này.</summary>
        bool IsDeleted { get; set; }

        /// <summary>Thời điểm bị xoá. null nghĩa là chưa xoá.</summary>
        DateTimeOffset? DeletedAt { get; set; }

        /// <summary>Id người thực hiện xoá (lấy từ ICurrentUser). null nếu do tiến trình nền.</summary>
        string? DeletedBy { get; set; }
    }
}
