using System.Collections;
using System.Text;
using System.Text.Json;
using document_service.Documents.Messages;
using document_service.Documents.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace document_service.Documents.Messaging;

public class DocumentUploadConsumer : BackgroundService
{
    private const int MaxRetries = 3;

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
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DocumentMessageNames.UploadRetryQueue
            });

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.UploadRetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DocumentMessageNames.UploadRequestedQueue
            });

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.UploadDeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<DocumentUploadRequested>(eventArgs.Body.Span)
                    ?? throw new InvalidOperationException("Mesajul de upload nu poate fi deserializat.");

                _logger.LogInformation(
                    "Am primit mesajul {MessageId} pentru todo-ul {TodoId}.",
                    message.MessageId,
                    message.TodoId);

                using var scope = _scopeFactory.CreateScope();
                var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                var documentCreatedPublisher = scope.ServiceProvider.GetRequiredService<IDocumentCreatedPublisher>();

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
            }
            catch (Exception exception)
            {
                var retryCount = GetRetryCount(eventArgs.BasicProperties.Headers);

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(
                        exception,
                        "Mesajul de upload a eșuat de {RetryCount} ori și este mutat în DLQ.",
                        retryCount);

                    await PublishToDeadLetterQueueAsync(channel, eventArgs);
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogWarning(
                    exception,
                    "Mesajul de upload a eșuat. Va fi reîncercat. Încercare {NextRetry}/{MaxRetries}.",
                    retryCount + 1,
                    MaxRetries);

                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false);
            }
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

    private static long GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null
            || !headers.TryGetValue("x-death", out var deaths)
            || deaths is not IEnumerable deadLetterEvents)
        {
            return 0;
        }

        foreach (var deadLetterEvent in deadLetterEvents)
        {
            if (deadLetterEvent is not IDictionary deadLetterTable
                || GetStringValue(deadLetterTable["queue"]) != DocumentMessageNames.UploadRequestedQueue)
            {
                continue;
            }

            if (long.TryParse(GetStringValue(deadLetterTable["count"]), out var retryCount))
            {
                return retryCount;
            }
        }

        return 0;
    }

    private static string? GetStringValue(object? value)
    {
        return value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : value?.ToString();
    }
    private static async Task PublishToDeadLetterQueueAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs)
    {
        var properties = new BasicProperties
        {
            ContentType = eventArgs.BasicProperties.ContentType,
            Persistent = true,
            Headers = eventArgs.BasicProperties.Headers is null
                ? null
                : new Dictionary<string, object?>(eventArgs.BasicProperties.Headers)
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: DocumentMessageNames.UploadDeadLetterQueue,
            mandatory: true,
            basicProperties: properties,
            body: eventArgs.Body);
    }
}


