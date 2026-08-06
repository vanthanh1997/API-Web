using Application.Common.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Runtime.CompilerServices;

namespace Infrastructure.Data
{
    public class SqlQuery(AppDbContext db) : ISqlQuery
    {
        /// <summary>
        /// Dùng lại connection string của EF — một nguồn cấu hình duy nhất,
        /// không phải khai lại ở appsettings.
        /// </summary>
        private SqlConnection CreateConnection()
            => new(db.Database.GetConnectionString());

        public async IAsyncEnumerable<T> StreamAsync<T>(
            string sql,
            object? parameters,
            bool isStoredProcedure,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);

            // Unbuffered = stream từng dòng. Nếu dùng QueryAsync thường,
            // Dapper nạp TOÀN BỘ vào List trước khi trả — đúng thứ ta đang tránh.
            var rows = connection.QueryUnbufferedAsync<T>(
                sql,
                parameters,
                commandType: isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text,
                commandTimeout: 300);          // báo cáo nặng có thể chạy lâu

            await foreach (var row in rows.WithCancellation(ct))
            {
                yield return row;
            }
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(
            string sql, object? parameters, bool isStoredProcedure, CancellationToken ct)
        {
            await using var connection = CreateConnection();

            var rows = await connection.QueryAsync<T>(new CommandDefinition(
                sql,
                parameters,
                commandType: isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text,
                commandTimeout: 120,
                cancellationToken: ct));

            return rows.ToList();
        }

        public async Task<T?> QuerySingleAsync<T>(
            string sql, object? parameters, bool isStoredProcedure, CancellationToken ct)
        {
            await using var connection = CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
                sql,
                parameters,
                commandType: isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text,
                commandTimeout: 120,
                cancellationToken: ct));
        }
    }
}
