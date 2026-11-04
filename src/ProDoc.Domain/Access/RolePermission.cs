namespace ProDoc.Domain.Access;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public static RolePermission Grant(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("A role is required.", nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("A permission is required.", nameof(permissionId));
        }

        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        };
    }
}
