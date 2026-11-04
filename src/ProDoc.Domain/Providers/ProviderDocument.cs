namespace ProDoc.Domain.Providers;

public sealed class ProviderDocument
{
    private ProviderDocument()
    {
    }

    public Guid Id { get; private set; }

    public Guid? DoctorId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public DocumentCategory Category { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public bool IsCurrent { get; private set; }

    public Guid? SupersedesDocumentId { get; private set; }

    public string ScanEngine { get; private set; } = string.Empty;

    public string ScanDetail { get; private set; } = string.Empty;

    public string? RejectionReason { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static bool IsRegulated(DocumentCategory category) =>
        category is DocumentCategory.MedicalDegree
            or DocumentCategory.RegistrationCertificate
            or DocumentCategory.Identity
            or DocumentCategory.ClinicRegistration;

    internal static ProviderDocument Create(
        Guid? doctorId,
        Guid? clinicId,
        DocumentCategory category,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        int version,
        Guid? supersedesDocumentId,
        string scanEngine,
        string scanDetail,
        DateTimeOffset now)
    {
        return new ProviderDocument
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            ClinicId = clinicId,
            Category = category,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Version = version,
            IsCurrent = true,
            SupersedesDocumentId = supersedesDocumentId,
            ScanEngine = scanEngine,
            ScanDetail = scanDetail,
            UploadedAtUtc = now
        };
    }

    internal void Supersede() => IsCurrent = false;

    internal void Reject(string reason) => RejectionReason = reason;
}
