using System.Text.Json;
using document_service.Documents.Messages;
using RabbitMQ.Client;

namespace document_service.Documents.Messaging;

public class RabbitMqDocumentDeletedPublisher : IDocumentDeletedPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqDocumentDeletedPublisher> _logger;

    public RabbitMqDocumentDeletedPublisher(IConnection connection, ILogger<RabbitMqDocumentDeletedPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync(DocumentDeleted message, CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.DeletedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties { ContentType = "application/json", Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: DocumentMessageNames.DeletedQueue,
            mandatory: true,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Mesajul DocumentDeleted {MessageId} a fost publicat pentru documentul {DocumentId} și todo-ul {TodoId}.",
            message.MessageId, message.DocumentId, message.TodoId);
    }
}
