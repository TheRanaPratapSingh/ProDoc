using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Providers;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class ReceptionistDoctorAssignmentConfiguration : IEntityTypeConfiguration<ReceptionistDoctorAssignment>
{
    public void Configure(EntityTypeBuilder<ReceptionistDoctorAssignment> builder)
    {
        builder.ToTable("ReceptionistDoctorAssignments");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ClinicId, item.ReceptionistUserId, item.DoctorId }).IsUnique();
        builder.HasIndex(item => new { item.ReceptionistUserId, item.ClinicId, item.IsActive });
        builder.Property(item => item.ClinicId).IsRequired();
        builder.Property(item => item.ReceptionistUserId).IsRequired();
        builder.Property(item => item.DoctorId).IsRequired();
    }
}
