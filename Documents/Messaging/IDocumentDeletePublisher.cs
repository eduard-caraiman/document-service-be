using document_service.Documents.Messages;

namespace document_service.Documents.Messaging;

public interface IDocumentDeletePublisher
{
    Task PublishAsync(
        DocumentDeleteRequested message,
        CancellationToken cancellationToken = default);
}