namespace document_service.Documents.Messages;

public class DocumentDeleted
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public int TodoId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
