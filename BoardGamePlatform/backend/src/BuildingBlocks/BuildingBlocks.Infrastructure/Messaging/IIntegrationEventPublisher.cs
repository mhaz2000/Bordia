using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events to the message bus (RabbitMQ).
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publishes an integration event to the bus.
    /// </summary>
    /// <param name="integrationEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
