using Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Behaviours
{
    public class TransactionBehaviour<TRequest, TResponse>(
        ITransaction transaction,
        ILogger<TRequest> logger) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            // Query chỉ đọc nên không cần transaction.
            if (request is not ICommandBase) return await next();

            // Command lồng nhau: dùng lại transaction đang mở, không mở thêm.
            if (transaction.HasActiveTransaction) return await next();

            await using var tx = await transaction.BeginAsync(ct);

            var response = await next();

            await tx.CommitAsync(ct);

            logger.LogDebug("Đã commit transaction cho {Request}.", typeof(TRequest).Name);

            return response;
        }
    }
}
