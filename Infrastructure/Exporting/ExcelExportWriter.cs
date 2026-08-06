using Application.Common.Exporting;
using ClosedXML.Excel;

namespace Infrastructure.Exporting
{
    public class ExcelExportWriter : IExportWriter
    {
        public ExportFormat Format => ExportFormat.Excel;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet(Truncate(definition.Title, 31));

            for (var i = 0; i < definition.Columns.Count; i++)
            {
                var column = definition.Columns[i];
                var cell = sheet.Cell(1, i + 1);

                cell.Value = column.Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                sheet.Column(i + 1).Width = column.Width;

                if (column.Format is not null)
                {
                    sheet.Column(i + 1).Style.NumberFormat.Format = column.Format;
                }
            }

            var row = 2;

            await foreach (var item in rows.WithCancellation(ct))
            {
                for (var i = 0; i < definition.Columns.Count; i++)
                {
                    SetValue(sheet.Cell(row, i + 1), definition.Columns[i].Value(item));
                }

                row++;
            }

            sheet.SheetView.FreezeRows(1);
            sheet.Range(1, 1, Math.Max(row - 1, 1), definition.Columns.Count).SetAutoFilter();

            // SaveAs là API ĐỒNG BỘ và .xlsx là gói zip cần seek lại khi đóng gói.
            // Response.Body vừa không seek được vừa chặn sync I/O ("Synchronous operations
            // are disallowed"), nên phải qua SeekableStreamAdapter thay vì ghi thẳng.
            await SeekableStreamAdapter.WriteViaSeekableAsync(
                output, stream => workbook.SaveAs(stream), ct);
        }

        /// <summary>
        /// Gán đúng KIỂU cho ô — số vào ô số, ngày vào ô ngày. Nếu để ToString() hết
        /// thì Excel không lọc/tính toán được, và người dùng sẽ phàn nàn ngay.
        /// </summary>
        private static void SetValue(IXLCell cell, object? value)
        {
            switch (value)
            {
                case null:
                    break;
                case decimal d:
                    cell.Value = d;
                    break;
                case double db:
                    cell.Value = db;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case bool b:
                    cell.Value = b;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    break;
                case DateTimeOffset dto:
                    cell.Value = dto.DateTime;
                    break;
                default:
                    cell.SetValue(value.ToString());
                    break;
            }
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max];
    }
}
