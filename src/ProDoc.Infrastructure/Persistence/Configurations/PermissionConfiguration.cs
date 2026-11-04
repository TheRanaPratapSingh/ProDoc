using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Access;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.Property(permission => permission.Code).HasMaxLength(80).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(200).IsRequired();
        builder.HasIndex(permission => permission.Code).IsUnique();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(grant => new { grant.RoleId, grant.PermissionId });
        builder.HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(grant => grant.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(grant => grant.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
