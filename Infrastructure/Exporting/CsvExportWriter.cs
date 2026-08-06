using Application.Common.Exporting;
using System.Globalization;
using System.Text;

namespace Infrastructure.Exporting
{
    public class CsvExportWriter : IExportWriter
    {
        public ExportFormat Format => ExportFormat.Csv;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            // BOM bắt buộc: không có nó Excel mở CSV tiếng Việt ra chữ rác.
            await using var writer = new StreamWriter(
                output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 64 * 1024, leaveOpen: true);

            await writer.WriteLineAsync(string.Join(',', definition.Columns.Select(c => Escape(c.Header))));

            var count = 0;
            var buffer = new StringBuilder(8 * 1024);

            await foreach (var item in rows.WithCancellation(ct))
            {
                for (var i = 0; i < definition.Columns.Count; i++)
                {
                    if (i > 0) buffer.Append(',');

                    buffer.Append(Escape(Render(definition.Columns[i].Value(item))));
                }

                buffer.Append('\n');

                // Gom nhiều dòng rồi mới ghi — giảm số lần chạm I/O.
                if (++count % 1000 == 0)
                {
                    await writer.WriteAsync(buffer, ct);
                    buffer.Clear();
                }
            }

            if (buffer.Length > 0)
            {
                await writer.WriteAsync(buffer, ct);
            }

            await writer.FlushAsync(ct);
        }

        private static string Render(object? value) => value switch
        {
            null => string.Empty,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double db => db.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss"),
            bool b => b ? "1" : "0",
            _ => value.ToString() ?? string.Empty
        };

        private static string Escape(string value)
        {
            // CSV injection: ô bắt đầu bằng = + - @ sẽ bị Excel chạy như công thức.
            // Thêm dấu nháy đơn ở đầu để Excel coi là text.
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            {
                value = "'" + value;
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
