namespace document_service.Documents.Repositories;

public interface IDocumentRepository
{
    Task<Document> CreateAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id);
    Task DeleteAsync(Document document);
    Task<IEnumerable<Document>> GetAllAsync();
}