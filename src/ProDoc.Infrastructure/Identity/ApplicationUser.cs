using Microsoft.AspNetCore.Identity;
using DomainPatient = ProDoc.Domain.Patients.Patient;

namespace ProDoc.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    public DomainPatient? Patient { get; set; }
}
