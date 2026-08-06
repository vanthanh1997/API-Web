using Application.Common.Exporting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
namespace Infrastructure.Exporting
{
    public class WordExportWriter : IExportWriter
    {
        public ExportFormat Format => ExportFormat.Word;

        public async Task WriteAsync<T>(
            Stream output,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            CancellationToken ct)
        {
            var items = new List<T>();

            await foreach (var item in rows.WithCancellation(ct))
            {
                items.Add(item);
            }

            // Response.Body chỉ ghi được, mà docx là gói zip cần đọc lại — xem SeekableStreamAdapter.
            await SeekableStreamAdapter.WriteViaSeekableAsync(
                output, stream => WriteCore(stream, definition, items), ct);
        }

        private void WriteCore<T>(Stream output, ExportDefinition<T> definition, List<T> rows)
        {
            using var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);

            var main = document.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(Heading(definition.Title));
            body.AppendChild(SubText($"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}"));

            var table = body.AppendChild(new Table(
                new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct })));

            var headerRow = new TableRow();

            foreach (var column in definition.Columns)
            {
                headerRow.AppendChild(Cell(column.Header, bold: true, shaded: true, ExportAlign.Center));
            }

            table.AppendChild(headerRow);

            var count = 0;

            foreach (var item in rows)
            {
                var row = new TableRow();

                foreach (var column in definition.Columns)
                {
                    row.AppendChild(Cell(Render(column.Value(item)), false, false, column.Align));
                }

                table.AppendChild(row);
                count++;
            }

            body.AppendChild(SubText($"Tổng {count:N0} dòng"));

            main.Document.Save();
        }

        private static Paragraph Heading(string text) => new(
            new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text(text)));

        private static Paragraph SubText(string text) => new(
            new Run(new RunProperties(new FontSize { Val = "16" }, new Color { Val = "808080" }),
                new Text(text)));

        private static TableCell Cell(string text, bool bold, bool shaded, ExportAlign align)
        {
            var runProps = new RunProperties(new FontSize { Val = "18" });

            if (bold) runProps.AppendChild(new Bold());

            var justify = align switch
            {
                ExportAlign.Right => JustificationValues.Right,
                ExportAlign.Center => JustificationValues.Center,
                _ => JustificationValues.Left
            };

            var cell = new TableCell(new Paragraph(
                new ParagraphProperties(new Justification { Val = justify }),
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));

            if (shaded)
            {
                cell.PrependChild(new TableCellProperties(
                    new Shading { Fill = "E5E7EB", Val = ShadingPatternValues.Clear }));
            }

            return cell;
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
