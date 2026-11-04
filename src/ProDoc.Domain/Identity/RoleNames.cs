namespace ProDoc.Domain.Identity;

public static class RoleNames
{
    public const string Patient = "Patient";

    public const string Doctor = "Doctor";

    public const string ClinicAdmin = "ClinicAdmin";

    public const string Receptionist = "Receptionist";

    public const string ClinicStaff = "ClinicStaff";

    public const string OrganizationAdmin = "OrganizationAdmin";

    public const string PlatformAdmin = "PlatformAdmin";

    public static IReadOnlyList<string> All { get; } =
    [
        Patient,
        Doctor,
        ClinicAdmin,
        Receptionist,
        ClinicStaff,
        OrganizationAdmin,
        PlatformAdmin
    ];
}
