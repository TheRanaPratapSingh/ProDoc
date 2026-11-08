using Microsoft.AspNetCore.Identity;
using DomainPatient = ProDoc.Domain.Patients.Patient;

namespace ProDoc.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const string AccountDeactivationReason = "Account deactivated by platform administrator";

    public const string PermanentDeletionReason = "Permanently deleted by platform administrator";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    /// <summary>
    /// Set when login credentials are permanently scrubbed. Clinical FKs may keep an anonymized shell row.
    /// </summary>
    public DateTimeOffset? PermanentlyDeletedAtUtc { get; set; }

    public DomainPatient? Patient { get; set; }

    public bool IsPermanentlyDeleted => PermanentlyDeletedAtUtc is not null;

    public bool IsInactive => SuspendedAtUtc is not null || PermanentlyDeletedAtUtc is not null;
}
