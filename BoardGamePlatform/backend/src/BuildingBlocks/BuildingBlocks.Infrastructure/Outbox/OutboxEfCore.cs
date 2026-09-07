using System.Text.Json;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutbox"/> backed by the service's DbContext.
/// </summary>
/// <typeparam name="TDbContext">The service-specific DbContext type.</typeparam>
public class OutboxEfCore<TDbContext> : IOutbox
    where TDbContext : AppDbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxEfCore{TDbContext}"/> class.
    /// </summary>
    public OutboxEfCore(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = integrationEvent.GetType().AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
            OccurredOn = DateTime.UtcNow
        };

        _dbContext.Set<OutboxMessage>().Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (message is not null)
        {
            message.ProcessedOn = DateTime.UtcNow;
            message.Error = null;
        }
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (message is not null)
        {
            message.Error = error;
        }
    }
}
