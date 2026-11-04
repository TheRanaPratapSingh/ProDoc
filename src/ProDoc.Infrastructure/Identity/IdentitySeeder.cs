using Microsoft.EntityFrameworkCore;
using ProDoc.Domain.Access;
using ProDoc.Domain.Identity;
using ProDoc.Infrastructure.Persistence;
using IdentityRole = Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>;

namespace ProDoc.Infrastructure.Identity;

public sealed class IdentitySeeder(ProDocDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleNames.All)
        {
            var normalized = roleName.ToUpperInvariant();
            if (!await db.Roles.AnyAsync(role => role.NormalizedName == normalized, cancellationToken))
            {
                db.Roles.Add(new IdentityRole
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = normalized,
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var existingCodes = await db.Permissions.Select(permission => permission.Code).ToListAsync(cancellationToken);
        foreach (var definition in PermissionCodes.Catalog)
        {
            if (!existingCodes.Contains(definition.Code, StringComparer.Ordinal))
            {
                db.Permissions.Add(Permission.Create(definition.Code, definition.Description));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var roles = await db.Roles.ToListAsync(cancellationToken);
        var permissions = await db.Permissions.ToListAsync(cancellationToken);
        var grants = await db.RolePermissions.ToListAsync(cancellationToken);

        foreach (var roleName in RoleNames.All)
        {
            var role = roles.Single(candidate => candidate.Name == roleName);
            var desired = PermissionCodes.RoleGrants[roleName].ToHashSet(StringComparer.Ordinal);
            var current = grants.Where(grant => grant.RoleId == role.Id).ToList();

            foreach (var grant in current)
            {
                var permission = permissions.Single(candidate => candidate.Id == grant.PermissionId);
                if (!desired.Contains(permission.Code))
                {
                    db.RolePermissions.Remove(grant);
                }
            }

            var currentCodes = current
                .Select(grant => permissions.Single(candidate => candidate.Id == grant.PermissionId).Code)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var code in desired.Except(currentCodes, StringComparer.Ordinal))
            {
                var permission = permissions.Single(candidate => candidate.Code == code);
                db.RolePermissions.Add(RolePermission.Grant(role.Id, permission.Id));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
