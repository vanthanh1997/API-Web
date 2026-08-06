using Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data
{
    public class SqlBulkWriter(AppDbContext db) : IBulkWriter
    {
        public async Task<int> BulkInsertAsync<T>(IEnumerable<T> rows, CancellationToken ct)
            where T : class
        {
            var entityType = db.Model.FindEntityType(typeof(T))
                ?? throw new InvalidOperationException($"{typeof(T).Name} không phải entity của AppDbContext.");

            var tableName = entityType.GetSchemaQualifiedTableName()
                ?? throw new InvalidOperationException($"Không xác định được bảng của {typeof(T).Name}.");

            // Chỉ lấy cột thật trong DB, bỏ navigation/computed.
            var properties = entityType.GetProperties()
                .Where(p => !p.IsShadowProperty() && p.PropertyInfo is not null)
                .ToList();

            using var table = new DataTable();

            foreach (var property in properties)
            {
                var type = property.ClrType;

                table.Columns.Add(
                    property.GetColumnName(),
                    Nullable.GetUnderlyingType(type) ?? type);
            }

            await using var connection = new SqlConnection(db.Database.GetConnectionString());
            await connection.OpenAsync(ct);

            using var bulk = new SqlBulkCopy(connection)
            {
                DestinationTableName = tableName,
                BatchSize = 5_000,          // gửi theo lô, tránh giữ transaction quá lâu
                BulkCopyTimeout = 300
            };

            // Map theo TÊN cột — không dựa vào thứ tự, vì thứ tự cột trong DB có thể khác.
            foreach (DataColumn column in table.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            var totalInserted = 0;

            // TỐI ƯU HIỆU NĂNG & RAM: Xử lý theo từng lô 5,000 dòng.
            // Giúp tiêu thụ RAM cố định ở mức rất thấp dù truyền vào hàng triệu dòng.
            foreach (var chunk in rows.Chunk(5_000))
            {
                table.Rows.Clear();

                foreach (var row in chunk)
                {
                    var values = new object?[properties.Count];

                    for (var i = 0; i < properties.Count; i++)
                    {
                        values[i] = properties[i].PropertyInfo!.GetValue(row) ?? DBNull.Value;
                    }

                    table.Rows.Add(values);
                }

                await bulk.WriteToServerAsync(table, ct);
                totalInserted += chunk.Length;
            }

            return totalInserted;
        }
    }
}
