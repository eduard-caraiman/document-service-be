namespace document_service.Documents.Services;

public interface IDocumentService
{
    Task<Document> CreateAsync(
        string fileName,
        string contentType,
        long size,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(Guid id);

    Task<(Document Document, Stream Content)?> DownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Document>> GetAllAsync();
}