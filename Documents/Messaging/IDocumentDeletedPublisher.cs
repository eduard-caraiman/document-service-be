using document_service.Documents.Messages;

namespace document_service.Documents.Messaging;

public interface IDocumentDeletedPublisher
{
    Task PublishAsync(DocumentDeleted message, CancellationToken cancellationToken = default);
}
