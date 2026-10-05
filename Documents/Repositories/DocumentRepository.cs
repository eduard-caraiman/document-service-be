using document_service.Database;
using Microsoft.EntityFrameworkCore;

namespace document_service.Documents.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly DocumentDbContext _dbContext;

    public DocumentRepository(DocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Document?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Documents.FindAsync(id);
    }

    public async Task<IEnumerable<Document>> GetAllAsync()
    {
        return await _dbContext.Documents.ToListAsync();
    }

    public async Task<Document> CreateAsync(Document document)
    {
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();
        return document;
    }


    public async Task DeleteAsync(Document document)
    {
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync();
    }
}