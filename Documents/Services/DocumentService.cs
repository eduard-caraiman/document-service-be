using document_service.Documents.Repositories;
using document_service.Storage;

namespace document_service.Documents.Services;

public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDocumentRepository _documentRepository;

    public DocumentService(
        ILogger<DocumentService> logger,
        IDocumentStorage documentStorage,
        IDocumentRepository documentRepository)
    {
        _logger = logger;
        _documentStorage = documentStorage;
        _documentRepository = documentRepository;
    }

    public async Task<Document?> GetByIdAsync(Guid id)
    {
        return await _documentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Document>> GetAllAsync()
    {
        return await _documentRepository.GetAllAsync();
    }

    public async Task<Document> CreateAsync(
        string fileName,
        string contentType,
        long size,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            StorageKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };
        
        
        await _documentStorage.SaveAsync(document.StorageKey, content, cancellationToken);


        try
        {
            await _documentRepository.CreateAsync(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Nu s-au putut salva metadatele documentului {DocumentId}.",
                document.Id);

            await _documentStorage.DeleteAsync(
                document.StorageKey,
                CancellationToken.None);

            throw;
        }


        return document;
    }


    public async Task<(Document Document, Stream Content)?> DownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id);
        if (document is null)
        {
            return null;
        }

        var content = await _documentStorage.OpenReadAsync(document.StorageKey, cancellationToken);

        return (document, content);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id);

        if (document is null)
        {
            return false;
        }


        // Sterg din Storage
        await _documentStorage.DeleteAsync(document.StorageKey, cancellationToken);
        // Sterg din baza de date
        await _documentRepository.DeleteAsync(document);
        return true;
    }
}