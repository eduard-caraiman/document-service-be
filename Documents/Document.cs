namespace document_service.Documents;

public class Document
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public required string StorageKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
