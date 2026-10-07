namespace document_service.Documents.Messages;

public class DocumentCreated
{
    public Guid MessageId { get; set; } = Guid.NewGuid();

    public int TodoId { get; set; }

    public Guid DocumentId { get; set; }

    public required string FileName { get; set; }

    public long Size { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}