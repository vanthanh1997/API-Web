using Application.Common.Exporting;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Infrastructure.Exporting
{
    public static class ExportEndpoint
    {
        /// <summary>
        /// Ghi file export thẳng ra response.
        /// </summary>
        /// <param name="rowCount">
        /// Tổng số dòng, dùng để so với <see cref="ExportFormatInfo.DirectExportLimit"/>.
        /// Truyền null nếu chưa biết (bỏ qua bước kiểm tra giới hạn).
        /// </param>
        public static async Task WriteFileAsync<T>(
            this ControllerBase controller,
            IExportService exportService,
            ExportFormat format,
            ExportDefinition<T> definition,
            IAsyncEnumerable<T> rows,
            int? rowCount,
            CancellationToken ct)
        {
            var effectiveFormat = ResolveFormat(format, rowCount);

            var response = controller.Response;

            response.ContentType = effectiveFormat.ContentType();

            // inline = mở luôn trong tab (PDF/HTML để xem trước);
            // attachment = bắt browser tải về (Excel/CSV/Word không xem trong tab được).
            var disposition = effectiveFormat.CanPreviewInline() ? "inline" : "attachment";
            var fileName = $"{definition.FileName}.{effectiveFormat.Extension()}";

            // filename* (RFC 5987) để tên file tiếng Việt có dấu không thành ký tự rác.
            response.Headers.ContentDisposition =
                $"{disposition}; filename=\"{Ascii(fileName)}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";

            // Tắt buffering: gửi từng khối xuống client ngay khi ghi được, thay vì
            // đợi dựng xong cả file. Người dùng thấy file bắt đầu tải sớm và server
            // không phải giữ toàn bộ nội dung.
            var buffering = controller.HttpContext.Features
                .Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            buffering?.DisableBuffering();

            await exportService.WriteAsync(response.Body, effectiveFormat, definition, rows, ct);
        }

        /// <summary>
        /// Dữ liệu vượt ngưỡng của định dạng đang chọn thì tự đổi sang định dạng
        /// chịu được (Excel -> ExcelStream, còn lại -> CSV) thay vì để OutOfMemory
        /// giữa lúc đang ghi response — lúc đó status 200 đã gửi đi rồi, client chỉ
        /// nhận được file hỏng mà không hiểu vì sao.
        /// </summary>
        private static ExportFormat ResolveFormat(ExportFormat format, int? rowCount)
        {
            if (rowCount is not { } count) return format;

            if (count <= format.DirectExportLimit()) return format;

            return format.SupportsHugeData() ? format : format.FallbackForHugeData();
        }

        /// <summary>
        /// Bản ASCII của tên file cho tham số filename= cũ: header HTTP chỉ chấp nhận
        /// ASCII, ký tự có dấu lọt vào sẽ làm hỏng cả header.
        /// </summary>
        private static string Ascii(string value)
            => string.Concat(value.Select(c => c < 128 && c != '"' ? c : '_'));
    }
}
