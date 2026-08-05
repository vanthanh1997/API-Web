using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Exporting
{
    /// <summary>
    /// Mỗi định dạng một writer. Thêm định dạng mới = thêm 1 class implement interface này,
    /// DI tự nhặt, không phải sửa IExportService.
    /// </summary>
    public interface IExportWriter
    {
        ExportFormat Format { get; }

        Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct);
    }
}
