namespace document_service.Documents.Messages;

public class DocumentUploadRequested
{
    public Guid MessageId { get; set; } = Guid.NewGuid();

    public int TodoId { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long Size { get; set; }

    public required byte[] Content { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}