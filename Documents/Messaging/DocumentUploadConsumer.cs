using System.Text.Json;
using document_service.Documents.Messages;
using document_service.Documents.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace document_service.Documents.Messaging;

public class DocumentUploadConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly ILogger<DocumentUploadConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DocumentUploadConsumer(
        IConnection connection,
        ILogger<DocumentUploadConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _connection = connection;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.UploadRequestedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var message = JsonSerializer
                .Deserialize<DocumentUploadRequested>(eventArgs.Body.Span) ?? throw new InvalidOperationException(
                "Mesajul de upload nu poate fi deserializat.");

            _logger.LogInformation("Am primit mesajul {MessageId} pentru todo-ul {TodoId}.",
                message.MessageId,
                message.TodoId);

            using var scope = _scopeFactory.CreateScope();

            var documentService = scope.ServiceProvider
                .GetRequiredService<IDocumentService>();

            var documentCreatedPublisher = scope.ServiceProvider
                .GetRequiredService<IDocumentCreatedPublisher>();


            using var content = new MemoryStream(message.Content);

            var document = await documentService.CreateAsync(
                message.FileName,
                message.ContentType,
                message.Size,
                content,
                stoppingToken);

            _logger.LogInformation("Documentul {DocumentId} a fost salvat.", document.Id);

            await documentCreatedPublisher.PublishAsync(
                new DocumentCreated
                {
                    TodoId = message.TodoId,
                    DocumentId = document.Id,
                    FileName = document.FileName,
                    Size = document.Size
                },
                stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        };

        _logger.LogInformation(
            "Consumer-ul ascultă queue-ul {QueueName}.",
            DocumentMessageNames.UploadRequestedQueue);

        await channel.BasicConsumeAsync(
            queue: DocumentMessageNames.UploadRequestedQueue,
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}