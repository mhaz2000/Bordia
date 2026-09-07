namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Base class for all integration events published to the message bus (RabbitMQ).
/// Integration events carry data that other services need, but contain no logic.
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event occurrence.
    /// </summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The type of the event, used for routing.
    /// </summary>
    public string EventType => GetType().Name;
}
