using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public sealed class ProviderDocumentService(IProviderStore store, IDocumentStorage storage, IUploadScanner scanner, IClock clock)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public async Task<StoredUpload> StoreAsync(IncomingUpload upload, bool imagesOnly, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await upload.Content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaxBytes)
            {
                throw DomainException.Validation("file_too_large", "Upload a file of 10 MB or smaller.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw DomainException.Validation("file_empty", "Choose a file to upload.");
        }

        buffer.Position = 0;
        var scan = await scanner.ScanAsync(buffer, upload.FileName, upload.ContentType, imagesOnly, cancellationToken);
        if (!scan.Allowed || string.IsNullOrWhiteSpace(scan.ContentType))
        {
            throw DomainException.Validation("file_rejected", scan.Detail);
        }

        buffer.Position = 0;
        var stored = await storage.SaveAsync(buffer, ExtensionFor(scan.ContentType), cancellationToken);
        return new StoredUpload(
            SafeFileName(upload.FileName),
            scan.ContentType,
            stored.SizeBytes,
            stored.StorageKey,
            scan.Engine,
            scan.Detail);
    }

    public async Task<DocumentDownload> OpenAsync(
        Guid userId,
        bool canReview,
        bool canSearch,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await store.FindDocumentAsync(documentId, cancellationToken)
            ?? throw DomainException.NotFound("document_not_found", "That document was not found.");
        if (!await CanReadAsync(userId, canReview, canSearch, document, cancellationToken))
        {
            throw DomainException.NotFound("document_not_found", "That document was not found.");
        }

        var stream = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        return new DocumentDownload(document.OriginalFileName, document.ContentType, stream);
    }

    private async Task<bool> CanReadAsync(
        Guid userId,
        bool canReview,
        bool canSearch,
        ProviderDocument document,
        CancellationToken cancellationToken)
    {
        if (canReview)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (document.DoctorId is Guid doctorId)
        {
            var doctor = await store.FindDoctorAsync(doctorId, tracking: false, cancellationToken);
            if (doctor is null)
            {
                return false;
            }

            if (doctor.UserId == userId)
            {
                return true;
            }

            return canSearch
                && document.IsCurrent
                && document.Category == DocumentCategory.ProfilePhoto
                && doctor.IsPubliclyBookable(today);
        }

        if (document.ClinicId is Guid clinicId)
        {
            var clinic = await store.FindClinicAsync(clinicId, tracking: false, cancellationToken);
            if (clinic is null)
            {
                return false;
            }

            if (clinic.OwnerUserId == userId)
            {
                return true;
            }

            return canSearch
                && document.IsCurrent
                && document.Category == DocumentCategory.ClinicLogo
                && clinic.IsPubliclyListed(today);
        }

        return false;
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => throw DomainException.Validation("file_rejected", "Upload a PDF, JPEG, PNG, or WebP file.")
    };

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            return "document";
        }

        var cleaned = new string(name.Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_').ToArray());
        if (cleaned.Length == 0)
        {
            return "document";
        }

        return cleaned.Length <= 120 ? cleaned : cleaned[..120];
    }
}
