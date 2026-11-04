using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reference).HasMaxLength(32);
        builder.Property(item => item.PatientName).HasMaxLength(80);
        builder.Property(item => item.TimeZoneId).HasMaxLength(64);
        builder.Property(item => item.ConsultationFee).HasPrecision(10, 2);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.PaymentMethod).HasMaxLength(32);
        builder.Property(item => item.PaymentStatus).HasMaxLength(24);
        builder.Property(item => item.PaidAmount).HasPrecision(10, 2);
        builder.Property(item => item.Reason).HasMaxLength(120);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.DecisionNote).HasMaxLength(120);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.Reference).IsUnique().HasFilter("[Reference] IS NOT NULL");
        builder.HasIndex(item => item.AccountUserId);
        builder.HasIndex(item => new { item.DoctorId, item.StartsAtUtc });
        builder.HasIndex(item => new { item.DoctorId, item.BranchId, item.StartsAtUtc })
            .IsUnique()
            .HasFilter("[Status] IN (1, 2, 3, 7, 8)");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsultationSchedule>().WithMany().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FamilyMember>().WithMany().HasForeignKey(item => item.FamilyMemberId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("FamilyMembers");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.FullName).HasMaxLength(80);
        builder.Property(item => item.Gender).HasMaxLength(32);
        builder.Property(item => item.Relationship).HasMaxLength(16);
        builder.HasIndex(item => item.AccountUserId);
    }
}
