using System.Text;
using System.Text.Json;
using BuildingBlocks.Contracts.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events to RabbitMQ using a topic exchange.
/// Instances are created per publish because IConnection/IModel are not thread-safe
/// and should be scoped; this publisher opens a short-lived connection per publish.
/// </summary>
public class RabbitMqPublisher : IIntegrationEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqPublisher"/> class.
    /// </summary>
    public RabbitMqPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions));

        var routingKey = SnakeCase(integrationEvent.EventType);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Type = integrationEvent.GetType().AssemblyQualifiedName;
        properties.CorrelationId = integrationEvent.EventId.ToString();

        channel.BasicPublish(
            exchange: _options.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Published integration event {EventType} ({EventId}) to exchange {Exchange} with key {RoutingKey}",
            integrationEvent.EventType,
            integrationEvent.EventId,
            _options.Exchange,
            routingKey);

        return Task.CompletedTask;
    }

    private static string SnakeCase(string value)
        => string.Concat(
            value.Select((c, i) => i > 0 && char.IsUpper(c)
                ? "_" + c
                : c.ToString()))
            .ToLowerInvariant();
}
