using System.Text.Json;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Background service that reliably delivers outbox messages to the message bus.
/// Polls for unprocessed messages, publishes each to RabbitMQ, and marks them processed on success.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxProcessor"/> class.
    /// </summary>
    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processor encountered an error");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var messages = await outbox.GetUnprocessedAsync(BatchSize, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var integrationEvent = Deserialize(message);
                if (integrationEvent is null)
                {
                    await outbox.MarkFailedAsync(
                        message.Id,
                        $"Unable to deserialize event type '{message.Type}'",
                        cancellationToken);
                    continue;
                }

                await publisher.PublishAsync(integrationEvent, cancellationToken);
                await outbox.MarkProcessedAsync(message.Id, cancellationToken);

                _logger.LogInformation(
                    "Delivered outbox message {MessageId} ({EventType})",
                    message.Id,
                    message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to deliver outbox message {MessageId}",
                    message.Id);
                await outbox.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }

        if (messages.Count > 0)
        {
            await scope.ServiceProvider
                .GetRequiredService<BuildingBlocks.Application.IUnitOfWork>()
                .SaveChangesAsync(cancellationToken);
        }
    }

    private static IntegrationEvent? Deserialize(OutboxMessage message)
    {
        var type = Type.GetType(message.Type);
        if (type is null || !typeof(IntegrationEvent).IsAssignableFrom(type))
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            message.Payload,
            type,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            as IntegrationEvent;
    }
}
