namespace document_service.Documents.Responses;

public class GetDocumentResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }

    public static GetDocumentResponse From(Document document)
    {
        return new GetDocumentResponse
        {
            Id = document.Id,
            FileName = document.FileName,
            ContentType = document.ContentType,
            Size = document.Size,
            CreatedAt = document.CreatedAt
        };
    }
}