using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Identity;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("StaffInvitations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PhoneE164).HasMaxLength(20).IsRequired();
        builder.Property(item => item.FullName).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Email).HasMaxLength(256);
        builder.Property(item => item.RoleName).HasMaxLength(64).IsRequired();
        builder.Property(item => item.PayloadJson).HasMaxLength(4000);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(item => new { item.PhoneE164, item.Status });
        builder.HasIndex(item => item.ExpiresAtUtc);
    }
}
