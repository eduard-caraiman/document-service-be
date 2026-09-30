namespace document_service.Documents.Repositories;

public interface IDocumentRepository
{
    Task<Document> CreateAsync(Document document);
}