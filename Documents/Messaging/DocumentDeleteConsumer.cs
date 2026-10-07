using System.Text.Json;
using document_service.Documents.Messages;
using document_service.Documents.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace document_service.Documents.Messaging;

public class DocumentDeleteConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly ILogger<DocumentDeleteConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DocumentDeleteConsumer(IConnection connection, ILogger<DocumentDeleteConsumer> logger, IServiceScopeFactory scopeFactory)
    {
        _connection = connection;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.DeleteRequestedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var message = JsonSerializer.Deserialize<DocumentDeleteRequested>(eventArgs.Body.Span)
                ?? throw new InvalidOperationException("Mesajul DocumentDeleteRequested nu poate fi deserializat.");

            using var scope = _scopeFactory.CreateScope();
            var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var documentDeletedPublisher = scope.ServiceProvider.GetRequiredService<IDocumentDeletedPublisher>();

            var deleted = await documentService.DeleteAsync(message.DocumentId, stoppingToken);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Documentul {DocumentId} nu mai există, dar confirmăm ștergerea pentru todo-ul {TodoId}.",
                    message.DocumentId, message.TodoId);
            }

            await documentDeletedPublisher.PublishAsync(
                new DocumentDeleted { TodoId = message.TodoId, DocumentId = message.DocumentId },
                stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        };

        _logger.LogInformation("Consumer-ul ascultă queue-ul {QueueName}.", DocumentMessageNames.DeleteRequestedQueue);

        await channel.BasicConsumeAsync(
            queue: DocumentMessageNames.DeleteRequestedQueue,
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
