using Application.Common.Interfaces;
using MassTransit;

namespace Infrastructure.Messaging
{
    public class MassTransitEventBus(IPublishEndpoint publishEndpoint) : IEventBus
    {
        public Task PublishAsync<T>(T message, CancellationToken ct) where T : class => publishEndpoint.Publish(message, ct);
    }
}
