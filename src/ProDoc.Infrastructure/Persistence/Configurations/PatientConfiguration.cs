using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Patients;
using ProDoc.Infrastructure.Identity;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.Property(patient => patient.FullName).HasMaxLength(Patient.MaxNameLength);
        builder.Property(patient => patient.Gender).HasMaxLength(32);
        builder.Property(patient => patient.PreferredLanguage).HasMaxLength(8).IsRequired();
        builder.Property(patient => patient.ProfileCompleted).IsRequired().HasDefaultValue(false);
        builder.Property(patient => patient.RowVersion).IsRowVersion();
        builder.HasIndex(patient => patient.UserId).IsUnique();
        builder.HasIndex(patient => patient.ProfileCompleted);
        builder.HasOne<ApplicationUser>()
            .WithOne(user => user.Patient)
            .HasForeignKey<Patient>(patient => patient.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
