using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Providers;

public sealed class ClinicStaffMember
{
    private ClinicStaffMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public Guid UserId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ClinicStaffMember Assign(
        Guid clinicId,
        Guid userId,
        Guid? branchId,
        string role,
        DateTimeOffset now)
    {
        var normalized = role.Trim();
        if (normalized is not (RoleNames.Receptionist or RoleNames.ClinicStaff))
        {
            throw DomainException.Validation("staff_role_invalid", "Choose Receptionist or ClinicStaff.");
        }

        return new ClinicStaffMember
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            BranchId = branchId,
            UserId = userId,
            Role = normalized,
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

    public void Reactivate(Guid? branchId, string role, DateTimeOffset now)
    {
        var normalized = role.Trim();
        if (normalized is not (RoleNames.Receptionist or RoleNames.ClinicStaff))
        {
            throw DomainException.Validation("staff_role_invalid", "Choose Receptionist or ClinicStaff.");
        }

        BranchId = branchId;
        Role = normalized;
        IsActive = true;
        UpdatedAtUtc = now;
    }

    public bool CoversBranch(Guid branchId) => BranchId is null || BranchId == branchId;
}
