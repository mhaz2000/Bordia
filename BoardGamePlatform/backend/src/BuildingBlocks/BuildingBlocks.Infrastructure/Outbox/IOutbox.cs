using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Abstraction for the transactional outbox. Implementations append integration events
/// to be delivered and expose unprocessed messages for the background processor.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Queues an integration event for reliable delivery.
    /// </summary>
    /// <param name="integrationEvent">The event to enqueue.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unprocessed outbox messages up to the specified limit.
    /// </summary>
    /// <param name="limit">The maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as successfully processed.
    /// </summary>
    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a processing error on an outbox message.
    /// </summary>
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
}
