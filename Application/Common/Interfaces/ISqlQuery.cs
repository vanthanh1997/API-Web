using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface ISqlQuery
    {
        /// <summary>
        /// Đọc theo kiểu STREAM — trả từng dòng, không nạp hết vào RAM.
        /// Dùng cho export cỡ trăm nghìn/triệu dòng.
        /// </summary>
        IAsyncEnumerable<T> StreamAsync<T>(
            string sql, object? parameters, bool isStoredProcedure, CancellationToken ct);

        /// <summary>Đọc trọn vào List — chỉ dùng khi biết chắc kết quả nhỏ.</summary>
        Task<IReadOnlyList<T>> QueryAsync<T>(
            string sql, object? parameters, bool isStoredProcedure, CancellationToken ct);

        Task<T?> QuerySingleAsync<T>(
            string sql, object? parameters, bool isStoredProcedure, CancellationToken ct);
    }
}
