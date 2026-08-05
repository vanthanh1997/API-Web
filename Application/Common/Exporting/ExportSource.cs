namespace Application.Common.Exporting
{
    /// <summary>
    /// Nguồn dữ liệu để xuất: dòng dạng stream + tổng số dòng + cách hiển thị.
    /// Không giữ dữ liệu trong RAM — rows chỉ được đọc khi writer duyệt qua.
    /// </summary>
    public record ExportSource<T>(IAsyncEnumerable<T> Rows, int Count, ExportDefinition<T> Definition);
}
