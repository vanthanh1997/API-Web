namespace Application.Common.Exporting
{
    public class ExportDefinition<T>
    {
        public string Title { get; init; } = "Báo cáo";

        public string FileName { get; init; } = "export";

        public List<ExportColumn<T>> Columns { get; } = [];
        public ExportDefinition<T> Column(string header, Func<T, object?> value, double width = 20, ExportAlign align = ExportAlign.Left, string? format = null)
        {
            Columns.Add(new ExportColumn<T>
            {
                Header = header,
                Value = value,
                Width = width,
                Align = align,
                Format = format
            });

            return this;
        }
    }
    public class ExportColumn<T>
    {
        public string Header { get; init; } = null!;

        public Func<T, object?> Value { get; init; } = null!;

        public double Width { get; init; } = 20;

        public ExportAlign Align { get; init; }

        /// <summary>Format của Excel, VD "#,##0" hoặc "dd/MM/yyyy".</summary>
        public string? Format { get; init; }
    }

    public enum ExportAlign
    {
        Left,
        Center,
        Right
    }
}
