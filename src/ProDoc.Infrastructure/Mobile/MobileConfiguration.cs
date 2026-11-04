using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Mobile;

namespace ProDoc.Infrastructure.Mobile;

public sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("PushDevices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
        builder.Property(x => x.AppVersion).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
