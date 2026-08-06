using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Messaging
{
    public class NoOpEventBus(ILogger<NoOpEventBus> logger) : IEventBus
    {
        public Task PublishAsync<T>(T message, CancellationToken ct) where T : class
        {
            logger.LogWarning(
                "Bỏ qua event {EventType}: chưa cấu hình ConnectionStrings:RabbitMq nên message bus không hoạt động.",
                typeof(T).Name);

            return Task.CompletedTask;
        }
    }
}
