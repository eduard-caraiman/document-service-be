using document_service.Documents.Messages;

namespace document_service.Documents.Messaging;

public interface IDocumentCreatedPublisher
{
    Task PublishAsync(DocumentCreated message, CancellationToken cancellationToken = default);
}