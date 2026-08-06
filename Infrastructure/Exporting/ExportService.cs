using Application.Common.Exporting;

namespace Infrastructure.Exporting
{
    public class ExportService(IEnumerable<IExportWriter> writers) : IExportService
    {
        private readonly Dictionary<ExportFormat, IExportWriter> _writers =
            writers.ToDictionary(w => w.Format);

        public Task WriteAsync<T>(
            Stream output,
            ExportFormat format,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            if (!_writers.TryGetValue(format, out var writer))
            {
                throw new NotSupportedException($"Chưa hỗ trợ xuất định dạng {format}.");
            }

            return writer.WriteAsync(output, definition, rows, ct);
        }
    }
}
