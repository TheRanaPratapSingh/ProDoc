namespace ProDoc.Application.Abstractions;

public interface IDocumentStorage
{
    Task<StoredFile> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, long SizeBytes);

public interface IUploadScanner
{
    Task<UploadScan> ScanAsync(Stream content, string fileName, string contentType, bool imagesOnly, CancellationToken cancellationToken);
}

public sealed record UploadScan(bool Allowed, string Engine, string Detail, string? ContentType);

public interface IVerificationNotifier
{
    void ApplicationSubmitted(string kind, string reference);
}
