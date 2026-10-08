namespace document_service.Documents.Messages;

public static class DocumentMessageNames
{
    public const string UploadRequestedQueue = "document.upload.requested";

    public const string CreatedQueue = "document.created";

    public const string DeleteRequestedQueue = "document.delete.requested";

    public const string DeletedQueue = "document.deleted";

    public const string UploadRetryQueue = "document.upload.requested.retry";

    public const string UploadDeadLetterQueue = "document.upload.requested.dead-letter";

    public const string DeleteRetryQueue = "document.delete.requested.retry";

    public const string DeleteDeadLetterQueue = "document.delete.requested.dead-letter";

    public const string CreatedRetryQueue = "document.created.retry";

    public const string DeletedRetryQueue = "document.deleted.retry";
}

