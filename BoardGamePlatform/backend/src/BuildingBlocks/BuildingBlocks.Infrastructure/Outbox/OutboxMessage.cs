namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Stores an integration event pending publication to the message bus.
/// The Outbox pattern guarantees reliable delivery: events are written to the same
/// database as business changes, then a background processor publishes them to RabbitMQ
/// and marks them processed.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique identifier of the outbox message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The fully qualified .NET type name of the integration event.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The JSON-serialized integration event payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the message was successfully published.
    /// Null if not yet processed.
    /// </summary>
    public DateTime? ProcessedOn { get; set; }

    /// <summary>
    /// Error detail if processing failed. Null if never attempted or succeeded.
    /// </summary>
    public string? Error { get; set; }
}
