namespace Application.Common.Exporting
{
    public enum ExportFormat
    {
        Excel,
        ExcelStream,
        Csv,
        Pdf,
        Word,
        Html
    }
    public static class ExportFormatInfo
    {
        public static string ContentType(this ExportFormat format) => format switch
        {
            ExportFormat.Excel or ExportFormat.ExcelStream
                => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ExportFormat.Csv => "text/csv; charset=utf-8",
            ExportFormat.Pdf => "application/pdf",
            ExportFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ExportFormat.Html => "text/html; charset=utf-8",
            _ => "application/octet-stream"
        };
        public static string Extension(this ExportFormat format) => format switch
        {
            ExportFormat.Excel or ExportFormat.ExcelStream => "xlsx",
            ExportFormat.Csv => "csv",
            ExportFormat.Pdf => "pdf",
            ExportFormat.Word => "docx",
            ExportFormat.Html => "html",
            _ => "bin"
        };
        /// <summary>Chỉ PDF và HTML mở được thẳng trong browser.</summary>
        public static bool CanPreviewInline(this ExportFormat format)
            => format is ExportFormat.Pdf or ExportFormat.Html;

        /// <summary>
        /// Số dòng tối đa còn xuất trực tiếp được; trên mức này thì đẩy qua Hangfire.
        /// Các con số lấy từ đo thật (xem README) — mỗi định dạng ngốn RAM rất khác nhau:
        /// HTML/CSV/ExcelStream ghi thẳng ra stream nên gần như không tốn RAM;
        /// Excel (ClosedXML) và Word dựng cả cây trong bộ nhớ; PDF phải giữ hết để phân trang.
        /// </summary>
        public static int DirectExportLimit(this ExportFormat format) => format switch
        {
            ExportFormat.Csv => 100_000,
            ExportFormat.Html => 50_000,
            ExportFormat.ExcelStream => 50_000,
            ExportFormat.Excel => 20_000,
            ExportFormat.Word => 5_000,
            ExportFormat.Pdf => 5_000,
            _ => 5_000
        };
        /// <summary>
        /// Định dạng nào chịu được cỡ triệu dòng (chạy nền). Excel/Word/PDF thì không.
        /// </summary>
        public static bool SupportsHugeData(this ExportFormat format)
            => format is ExportFormat.Csv or ExportFormat.ExcelStream;
        /// <summary>
        /// Với dữ liệu cực lớn, tự đổi sang định dạng chịu được thay vì để OutOfMemory.
        /// </summary>
        public static ExportFormat FallbackForHugeData(this ExportFormat format) => format switch
        {
            ExportFormat.Excel => ExportFormat.ExcelStream,
            _ => ExportFormat.Csv
        };

    }
}
