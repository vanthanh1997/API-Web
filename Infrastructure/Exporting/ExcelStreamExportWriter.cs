using Application.Common.Exporting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Infrastructure.Exporting
{
    /// <summary>
    /// Excel cho dữ liệu RẤT LỚN (hàng trăm nghìn tới 1 triệu dòng).
    /// Dùng OpenXmlWriter (SAX) ghi thẳng XML ra đĩa — RAM gần như KHÔNG tăng theo số dòng,
    /// khác hẳn <see cref="ExcelExportWriter"/> (ClosedXML) vốn giữ cả workbook trong bộ nhớ.
    ///
    /// Đánh đổi: không có AutoFilter/freeze/style phong phú như ClosedXML.
    /// Dùng ClosedXML cho báo cáo nhỏ đẹp; dùng cái này khi số dòng lớn.
    /// </summary>
    public class ExcelStreamExportWriter : IExportWriter
    {
        /// <summary>Giới hạn cứng của định dạng .xlsx — vượt là file hỏng, Excel không mở được.</summary>
        public const int MaxRowsPerSheet = 1_048_575;   // 1.048.576 trừ 1 dòng header

        public ExportFormat Format => ExportFormat.ExcelStream;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            // Gom trước rồi mới ghi: OpenXmlWriter là API đồng bộ, không nhận IAsyncEnumerable.
            // Đây là điểm đánh đổi — xem README mục hiệu năng.
            var items = new List<T>();

            await foreach (var item in rows.WithCancellation(ct))
            {
                items.Add(item);
            }

            await SeekableStreamAdapter.WriteViaSeekableAsync(
                output, stream => WriteCore(stream, definition, items), ct);
        }

        private void WriteCore<T>(Stream output, ExportDefinition<T> definition, List<T> rows)
        {
            using var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);

            var workbookPart = document.AddWorkbookPart();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();

            var sheetCount = 0;
            var sheetIds = new List<(string Id, string Name)>();

            var writer = OpenXmlWriter.Create(worksheetPart);
            StartSheet(writer, definition);
            sheetCount++;
            sheetIds.Add((workbookPart.GetIdOfPart(worksheetPart), SheetName(definition.Title, sheetCount)));

            var rowIndex = 1u;   // dòng 1 là header

            foreach (var item in rows)
            {
                // Tràn giới hạn .xlsx -> sang sheet mới thay vì tạo file hỏng.
                if (rowIndex > MaxRowsPerSheet)
                {
                    EndSheet(writer);
                    writer.Close();

                    worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    writer = OpenXmlWriter.Create(worksheetPart);
                    StartSheet(writer, definition);

                    sheetCount++;
                    sheetIds.Add((workbookPart.GetIdOfPart(worksheetPart), SheetName(definition.Title, sheetCount)));
                    rowIndex = 1u;
                }

                rowIndex++;

                writer.WriteStartElement(new Row { RowIndex = rowIndex });

                foreach (var column in definition.Columns)
                {
                    WriteCell(writer, column.Value(item));
                }

                writer.WriteEndElement();
            }

            EndSheet(writer);
            writer.Close();

            // Sheets phải khai SAU khi ghi xong dữ liệu vì tới lúc đó mới biết có mấy sheet.
            var sheets = new Sheets();

            for (var i = 0; i < sheetIds.Count; i++)
            {
                sheets.AppendChild(new Sheet
                {
                    Id = sheetIds[i].Id,
                    SheetId = (uint)(i + 1),
                    Name = sheetIds[i].Name
                });
            }

            workbookPart.Workbook = new Workbook(sheets);
            workbookPart.Workbook.Save();
        }

        private static void StartSheet<T>(OpenXmlWriter writer, ExportDefinition<T> definition)
        {
            writer.WriteStartElement(new Worksheet());
            writer.WriteStartElement(new SheetData());

            writer.WriteStartElement(new Row { RowIndex = 1u });

            foreach (var column in definition.Columns)
            {
                writer.WriteElement(new Cell
                {
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(column.Header))
                });
            }

            writer.WriteEndElement();
        }

        private static void EndSheet(OpenXmlWriter writer)
        {
            writer.WriteEndElement();   // SheetData
            writer.WriteEndElement();   // Worksheet
        }

        /// <summary>
        /// Số ghi dạng Number để Excel còn lọc/tính SUM được; chuỗi dùng InlineString
        /// (không cần SharedStringTable — bảng đó phải giữ trong RAM, đúng thứ ta đang tránh).
        /// </summary>
        private static void WriteCell(OpenXmlWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteElement(new Cell());
                    break;

                case decimal or double or int or long or float:
                    writer.WriteElement(new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)
                    });
                    break;

                case bool b:
                    writer.WriteElement(new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(b ? "1" : "0")
                    });
                    break;

                case DateTime dt:
                    writer.WriteElement(new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(dt.ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture))
                    });
                    break;

                case DateTimeOffset dto:
                    writer.WriteElement(new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(dto.DateTime.ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture))
                    });
                    break;

                default:
                    writer.WriteElement(new Cell
                    {
                        DataType = CellValues.InlineString,
                        InlineString = new InlineString(new Text(value.ToString() ?? string.Empty))
                    });
                    break;
            }
        }

        private static string SheetName(string title, int index)
        {
            var name = title.Length > 25 ? title[..25] : title;

            return index == 1 ? name : $"{name} ({index})";
        }
    }
}
