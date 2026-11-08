namespace ProDoc.Domain.Identity;

public static class DashboardResolver
{
    /// <summary>
    /// Backend-assigned surface for post-login redirection. Never chosen by the client.
    /// When multiple privileged surfaces apply, <see cref="ListSurfaces"/> exposes them for selection.
    /// </summary>
    public static string Resolve(IReadOnlyList<string> roles)
    {
        var surfaces = ListSurfaces(roles);
        return surfaces.Count > 0 ? surfaces[0] : "patient";
    }

    /// <summary>
    /// Dashboards the account may open, ordered by privilege (highest first).
    /// The Patient surface is omitted when a privileged clinical/admin role is present so a
    /// Doctor/Receptionist/Staff number opens that workspace directly. Multi-role accounts
    /// (e.g. Doctor + Receptionist) still receive every privileged surface for selection.
    /// </summary>
    public static IReadOnlyList<string> ListSurfaces(IReadOnlyList<string> roles)
    {
        var surfaces = new List<string>();

        if (roles.Contains(RoleNames.PlatformAdmin, StringComparer.Ordinal))
        {
            surfaces.Add("admin");
        }

        if (roles.Contains(RoleNames.Doctor, StringComparer.Ordinal))
        {
            surfaces.Add("doctor");
        }

        if (roles.Contains(RoleNames.ClinicAdmin, StringComparer.Ordinal)
            || roles.Contains(RoleNames.OrganizationAdmin, StringComparer.Ordinal))
        {
            surfaces.Add("clinic");
        }

        if (roles.Contains(RoleNames.Receptionist, StringComparer.Ordinal))
        {
            surfaces.Add("reception");
        }

        if (roles.Contains(RoleNames.ClinicStaff, StringComparer.Ordinal))
        {
            surfaces.Add("staff");
        }

        if (surfaces.Count == 0)
        {
            surfaces.Add("patient");
        }

        return surfaces;
    }
}
