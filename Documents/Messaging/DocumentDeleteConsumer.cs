using System.Collections;
using System.Text;
using System.Text.Json;
using document_service.Documents.Messages;
using document_service.Documents.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace document_service.Documents.Messaging;

public class DocumentDeleteConsumer : BackgroundService
{
    private const int MaxRetries = 3;

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
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DocumentMessageNames.DeleteRetryQueue
            });

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.DeleteRetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DocumentMessageNames.DeleteRequestedQueue
            });

        await channel.QueueDeclareAsync(
            queue: DocumentMessageNames.DeleteDeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
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
                        message.DocumentId,
                        message.TodoId);
                }

                await documentDeletedPublisher.PublishAsync(
                    new DocumentDeleted { TodoId = message.TodoId, DocumentId = message.DocumentId },
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
                        "Mesajul de ștergere a eșuat de {RetryCount} ori și este mutat în DLQ.",
                        retryCount);

                    await PublishToDeadLetterQueueAsync(channel, eventArgs);
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogWarning(
                    exception,
                    "Mesajul de ștergere a eșuat. Va fi reîncercat. Încercare {NextRetry}/{MaxRetries}.",
                    retryCount + 1,
                    MaxRetries);

                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _logger.LogInformation("Consumer-ul ascultă queue-ul {QueueName}.", DocumentMessageNames.DeleteRequestedQueue);

        await channel.BasicConsumeAsync(
            queue: DocumentMessageNames.DeleteRequestedQueue,
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
                || GetStringValue(deadLetterTable["queue"]) != DocumentMessageNames.DeleteRequestedQueue)
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
        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString();
    }

    private static async Task PublishToDeadLetterQueueAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
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
            routingKey: DocumentMessageNames.DeleteDeadLetterQueue,
            mandatory: true,
            basicProperties: properties,
            body: eventArgs.Body);
    }
}
