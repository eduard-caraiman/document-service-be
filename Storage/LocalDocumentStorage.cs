namespace document_service.Storage;

public class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _rootPath;
    private readonly ILogger _logger;

    // configuration oferă acces la setări; environment oferă rădăcina aplicației.
    public LocalDocumentStorage(IConfiguration configuration, IWebHostEnvironment environment,
        ILogger<LocalDocumentStorage> logger)
    {
        _logger = logger;

        // 1. Citește proprietatea RootPath din secțiunea Storage (de exemplu, "uploads").
        var rootPath = configuration["Storage:RootPath"];

        // 2. Verifică dacă setarea lipsește, este goală sau conține doar spații.
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            // Oprește construirea instanței și explică ce configurare lipsește.
            throw new InvalidOperationException("Storage:RootPath trebuie configurat");
        }

        // 3. Combine unește rădăcina aplicației cu folderul configurat.
        // Dacă rootPath este deja o cale absolută, Combine o folosește pe aceasta.
        // GetFullPath normalizează rezultatul într-o cale absolută.
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, rootPath));

        // 4. Creează folderul și directoarele părinte necesare.
        // Dacă folderul există deja, nu îi șterge și nu îi modifică fișierele.
        Directory.CreateDirectory(_rootPath);
    }


    private string GetFilePath(string storageKey)
    {
        if (!Guid.TryParseExact(storageKey, "N", out _))
        {
            throw new ArgumentException("StorageKey must contain Guid in format N", nameof(storageKey));
        }

        return Path.Combine(_rootPath, storageKey);
    }

    // Va salva datele din fluxul content într-un fișier identificat prin storageKey.
    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        var fileCreated = false;

        try
        {
            await using var destination = new FileStream(filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            fileCreated = true;

            await content.CopyToAsync(destination, cancellationToken);
        }
        catch
        {
            if (fileCreated)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex,
                        "Nu s-a putut șterge fișierul incomplet {StorageKey}", storageKey);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex,
                        "Acces refuzat la ștergerea fișierului incomplet {StorageKey}", storageKey);
                }
            }

            throw;
        }
    }

    // Va deschide fișierul identificat prin storageKey și va returna un flux pentru citire.
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        await using var fileStream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return await Task.FromResult<Stream>(fileStream);
    }

    // Va elimina conținutul de pe disc pentru cheia primită.
    // Ștergerea metadatelor din baza de date este responsabilitatea unei alte componente.
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}