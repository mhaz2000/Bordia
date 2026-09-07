namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Configuration options for the RabbitMQ connection.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    /// The RabbitMQ host name.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The RabbitMQ AMQP port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// The RabbitMQ user name.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// The RabbitMQ password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// The topic exchange to which events are published.
    /// </summary>
    public string Exchange { get; set; } = "boardgame.integration";
}
