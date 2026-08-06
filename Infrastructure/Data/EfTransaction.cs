using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
namespace Infrastructure.Data
{
    public class EfTransaction(AppDbContext db) : ITransaction
    {
        public bool HasActiveTransaction => db.Database.CurrentTransaction is not null;

        public async Task<IDisposableTransaction> BeginAsync(CancellationToken ct)
            => new EfDisposableTransaction(await db.Database.BeginTransactionAsync(ct));
    }

    public class EfDisposableTransaction(IDbContextTransaction tx) : IDisposableTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        // Dispose mà chưa Commit thì EF tự rollback — không cần bắt exception thủ công.
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
