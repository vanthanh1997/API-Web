using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IBulkWriter
    {
        /// <summary>Chèn hàng loạt. Trả về số dòng đã ghi.</summary>
        Task<int> BulkInsertAsync<T>(IEnumerable<T> rows, CancellationToken ct) where T : class;
    }
}
