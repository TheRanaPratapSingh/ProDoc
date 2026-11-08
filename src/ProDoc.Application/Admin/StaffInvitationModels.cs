using ProDoc.Domain.Identity;

namespace ProDoc.Application.Admin;

public sealed record StaffInvitationDetails(
    Guid Id,
    string PhoneNumber,
    string FullName,
    string? Email,
    string RoleName,
    Guid? ClinicId,
    Guid? BranchId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    Guid? SpecializationId = null,
    int? YearsOfExperience = null,
    string? Qualifications = null,
    string? RegistrationNumber = null,
    string? Designation = null,
    IReadOnlyList<Guid>? ClinicIds = null,
    IReadOnlyList<Guid>? DoctorIds = null,
    DateTimeOffset? OtpExpiresAtUtc = null,
    string? DevelopmentCode = null,
    Guid? OtpChallengeId = null);

public sealed record StaffInviteActivationResult(
    Guid InvitationId,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string RoleName,
    IReadOnlyList<Guid> ClinicIds,
    Guid? DoctorId = null,
    string? DoctorStatus = null,
    bool IsPubliclyDiscoverable = false);

public sealed record InviteStaffCommand(
    Guid ActorUserId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string RoleName,
    Guid? ClinicId,
    Guid? BranchId,
    string? IpAddress,
    string? UserAgent,
    Guid? SpecializationId = null,
    int? YearsOfExperience = null,
    string? Qualifications = null,
    string? RegistrationNumber = null,
    string? Designation = null,
    IReadOnlyList<Guid>? ClinicIds = null,
    IReadOnlyList<Guid>? DoctorIds = null);

public static class StaffInviteRoles
{
    public static readonly IReadOnlyList<string> Allowed =
    [
        RoleNames.Doctor,
        RoleNames.ClinicAdmin,
        RoleNames.Receptionist,
        RoleNames.ClinicStaff
    ];

    public static string Normalize(string roleName)
    {
        var trimmed = roleName.Trim();
        if (trimmed.Equals("DoctorAssistant", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
        {
            return RoleNames.ClinicStaff;
        }

        if (trimmed.Equals("ClinicManager", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            return RoleNames.ClinicAdmin;
        }

        return Allowed.FirstOrDefault(role => role.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
               ?? throw Domain.Common.DomainException.Validation(
                   "invite_role_invalid",
                   "Choose Doctor, ClinicAdmin, Receptionist, or ClinicStaff.");
    }
}
