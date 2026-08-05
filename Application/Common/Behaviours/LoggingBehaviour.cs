using Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
namespace Application.Common.Behaviours
{
    public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            var name = typeof(TRequest).Name;
            var timer = Stopwatch.StartNew();

            try
            {
                var response = await next();

                timer.Stop();
                logger.LogInformation("Shop Request: {Name} ({Elapsed} ms) UserId={UserId}",
                    name, timer.ElapsedMilliseconds, currentUser.Id);

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Shop Request lỗi: {Name} {@Request} UserId={UserId}",
                    name, request, currentUser.Id);
                throw;
            }
        }
    }
}
