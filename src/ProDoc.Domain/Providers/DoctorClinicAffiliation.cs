using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

public sealed class DoctorClinicAffiliation
{
    private DoctorClinicAffiliation()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public AssociationKind Kind { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DoctorClinicAffiliation Create(
        Guid doctorId,
        Guid clinicId,
        Guid? branchId,
        AssociationKind kind,
        bool isPrimary,
        DateTimeOffset now)
    {
        if (doctorId == Guid.Empty || clinicId == Guid.Empty)
        {
            throw DomainException.Validation("affiliation_invalid", "Choose a doctor and a clinic.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw DomainException.Validation("affiliation_invalid", "Choose how this doctor practices at the clinic.");
        }

        return new DoctorClinicAffiliation
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            Kind = kind,
            IsPrimary = isPrimary,
            CreatedAtUtc = now
        };
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
