using Application.Common.Exporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace Infrastructure.Exporting
{
    public class PdfExportWriter : IExportWriter
    {
        public ExportFormat Format => ExportFormat.Pdf;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            // QuestPDF phải biết trước toàn bộ dữ liệu để tính layout/phân trang,
            // nên không stream được như Excel/HTML. Đây là lý do PDF nên giới hạn số dòng
            // hoặc đẩy qua Hangfire.
            var items = new List<T>();

            await foreach (var item in rows.WithCancellation(ct))
            {
                items.Add(item);
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(definition.Columns.Count > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(definition.Title).FontSize(16).SemiBold();
                        col.Item().Text($"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm} — {items.Count:N0} dòng")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(6);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            foreach (var column in definition.Columns)
                            {
                                cols.RelativeColumn((float)column.Width);
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var column in definition.Columns)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten2)
                                    .Padding(4)
                                    .Text(column.Header).SemiBold();
                            }
                        });

                        foreach (var item in items)
                        {
                            foreach (var column in definition.Columns)
                            {
                                var cell = table.Cell()
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4);

                                var text = cell.Text(Render(column.Value(item)));

                                if (column.Align == ExportAlign.Right) text.AlignRight();
                                else if (column.Align == ExportAlign.Center) text.AlignCenter();
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" / ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
                });
            });

            // GeneratePdf là API ĐỒNG BỘ: nó gọi Stream.Write thẳng vào đích.
            // Ghi trực tiếp vào Response.Body sẽ nổ "Synchronous operations are disallowed"
            // vì Kestrel chặn sync I/O. Đẩy qua SeekableStreamAdapter để ghi vào buffer
            // (RAM, tự tràn ra file tạm khi lớn) rồi copy sang đích bằng CopyToAsync.
            await SeekableStreamAdapter.WriteViaSeekableAsync(
                output, stream => document.GeneratePdf(stream), ct);
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
    }
}
