using Application.Common.Exporting;
using System.Net;
using System.Text;

namespace Infrastructure.Exporting
{
    public class HtmlExportWriter : IExportWriter
    {
        private const string Css = """
        <style>
          body{font-family:system-ui,-apple-system,'Segoe UI',sans-serif;margin:24px;color:#111}
          h2{margin:0 0 4px} .meta{color:#666;font-size:13px;margin-bottom:16px}
          table{border-collapse:collapse;width:100%;font-size:14px}
          th,td{border:1px solid #e5e7eb;padding:6px 10px}
          th{background:#f3f4f6;text-align:center;position:sticky;top:0}
          tr:nth-child(even) td{background:#fafafa}
          .r{text-align:right} .c{text-align:center}
        </style>
        """;

        public ExportFormat Format => ExportFormat.Html;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

            await writer.WriteAsync(
                "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\">"
                + $"<title>{Esc(definition.Title)}</title>"
                + Css
                + "</head><body>"
                + $"<h2>{Esc(definition.Title)}</h2>"
                + $"<div class=\"meta\">Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}</div>"
                + "<table><thead><tr>");

            foreach (var column in definition.Columns)
            {
                await writer.WriteAsync($"<th>{Esc(column.Header)}</th>");
            }

            await writer.WriteAsync("</tr></thead><tbody>");

            var count = 0;

            await foreach (var item in rows.WithCancellation(ct))
            {
                await writer.WriteAsync("<tr>");

                foreach (var column in definition.Columns)
                {
                    var css = column.Align switch
                    {
                        ExportAlign.Right => " class=\"r\"",
                        ExportAlign.Center => " class=\"c\"",
                        _ => string.Empty
                    };

                    await writer.WriteAsync($"<td{css}>{Esc(Render(column.Value(item)))}</td>");
                }

                await writer.WriteAsync("</tr>");

                // Xả bớt định kỳ để không phình buffer khi dữ liệu lớn.
                if (++count % 500 == 0)
                {
                    await writer.FlushAsync(ct);
                }
            }

            await writer.WriteAsync($"</tbody></table><div class=\"meta\">Tổng {count:N0} dòng</div></body></html>");
            await writer.FlushAsync(ct);
        }

        private static string Render(object? value) => value switch
        {
            null => string.Empty,
            decimal d => d.ToString("N0"),
            double db => db.ToString("N2"),
            DateTime dt => dt.ToString("dd/MM/yyyy"),
            DateTimeOffset dto => dto.ToString("dd/MM/yyyy"),
            bool b => b ? "Có" : "Không",
            _ => value.ToString() ?? string.Empty
        };

        /// <summary>Chống XSS: tên sản phẩm có &lt;script&gt; cũng không chạy được.</summary>
        private static string Esc(string value) => WebUtility.HtmlEncode(value);
    }
}
