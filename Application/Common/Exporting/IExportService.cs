using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Exporting
{
    /// <summary>
    /// Xuất dữ liệu ra file. Nhận IAsyncEnumerable để KHÔNG phải nạp hết dữ liệu vào RAM,
    /// và ghi thẳng vào Stream đích (response hoặc file) thay vì dựng byte[] trong bộ nhớ.
    /// </summary>
    public interface IExportService
    {
        Task WriteAsync<T>(
            Stream output,
            ExportFormat format,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct);
    }
}
