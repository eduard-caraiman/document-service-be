using System.Text.Json;
using document_service.Documents.Messages;
using RabbitMQ.Client;

namespace document_service.Documents.Messaging;

public class RabbitMqDocumentCreatedPublisher : IDocumentCreatedPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqDocumentCreatedPublisher> _logger;

    public RabbitMqDocumentCreatedPublisher(
        IConnection connection,
        ILogger<RabbitMqDocumentCreatedPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync(DocumentCreated message, CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.CreatedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: DocumentMessageNames.CreatedQueue,
            mandatory: true,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Mesajul de DocumentCreated {MessageId} a fost publicat pentru documentul {DocumentId} si todo-ul {TodoId}.",
            message.MessageId,
            message.DocumentId,
            message.TodoId);
    }
}