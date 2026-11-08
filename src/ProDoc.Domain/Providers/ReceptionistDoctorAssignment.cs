using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

/// <summary>
/// Restricts a receptionist to specific doctors within a clinic.
/// Empty coverage for a clinic means all affiliated doctors (legacy); once any
/// assignment exists for that receptionist+clinic, only listed doctors are visible.
/// </summary>
public sealed class ReceptionistDoctorAssignment
{
    private ReceptionistDoctorAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid ReceptionistUserId { get; private set; }

    public Guid DoctorId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ReceptionistDoctorAssignment Assign(
        Guid clinicId,
        Guid receptionistUserId,
        Guid doctorId,
        DateTimeOffset now)
    {
        if (clinicId == Guid.Empty || receptionistUserId == Guid.Empty || doctorId == Guid.Empty)
        {
            throw DomainException.Validation("assignment_invalid", "Choose a clinic, receptionist, and doctor.");
        }

        return new ReceptionistDoctorAssignment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            ReceptionistUserId = receptionistUserId,
            DoctorId = doctorId,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAtUtc = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAtUtc = now;
    }
}
