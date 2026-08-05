using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface ITransaction
    {
        Task<IDisposableTransaction> BeginAsync(CancellationToken ct);

        bool HasActiveTransaction { get; }
    }
    public interface IDisposableTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct);
    }
}
