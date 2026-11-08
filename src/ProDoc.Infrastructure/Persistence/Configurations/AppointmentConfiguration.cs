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
        builder.Property(item => item.TokenLabel).HasMaxLength(16);
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
        builder.Property(item => item.LocationClinicName).HasMaxLength(160);
        builder.Property(item => item.LocationBranchName).HasMaxLength(120);
        builder.Property(item => item.LocationAddress).HasMaxLength(500);
        builder.Property(item => item.LocationLatitude).HasPrecision(9, 6);
        builder.Property(item => item.LocationLongitude).HasPrecision(9, 6);
        builder.Property(item => item.LocationGooglePlaceId).HasMaxLength(256);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.Reference).IsUnique().HasFilter("[Reference] IS NOT NULL");
        builder.HasIndex(item => new { item.ClinicId, item.BranchId, item.DoctorId, item.TokenLabel, item.StartsAtUtc });
        builder.HasIndex(item => item.AccountUserId);
        builder.HasIndex(item => new { item.DoctorId, item.StartsAtUtc });
        // Timed-slot mode: exclusive occupancy at an exact start. Hourly queues allow many bookings per range.
        builder.HasIndex(item => new { item.DoctorId, item.BranchId, item.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("IX_Appointments_DoctorId_BranchId_StartsAtUtc_Timed")
            .HasFilter("[Status] IN (1, 2, 3, 7, 8) AND [BookingMode] = 0");
        builder.HasIndex(item => new { item.DoctorId, item.BranchId, item.StartsAtUtc, item.TokenLabel })
            .IsUnique()
            .HasDatabaseName("IX_Appointments_HourlyToken")
            .HasFilter("[Status] IN (2, 3, 7, 8) AND [BookingMode] = 1 AND [TokenLabel] IS NOT NULL");
        builder.Property(item => item.BookingMode).HasDefaultValue(ScheduleBookingMode.TimedSlots);
        // One active booking per patient + doctor + clinic-local calendar day.
        // Held(1)/Requested(2)/Confirmed(3)/CheckedIn(7). Expired/cancelled/completed free the day.
        builder.HasIndex(item => new { item.AccountUserId, item.DoctorId, item.ClinicLocalDate })
            .IsUnique()
            .HasDatabaseName("IX_Appointments_AccountUserId_DoctorId_ClinicLocalDate")
            .HasFilter("[Status] IN (1, 2, 3, 7)");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsultationSchedule>().WithMany().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FamilyMember>().WithMany().HasForeignKey(item => item.FamilyMemberId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AppointmentShareLinkConfiguration : IEntityTypeConfiguration<AppointmentShareLink>
{
    public void Configure(EntityTypeBuilder<AppointmentShareLink> builder)
    {
        builder.ToTable("AppointmentShareLinks");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(item => item.TokenHash).IsUnique();
        builder.HasIndex(item => item.AppointmentId);
        builder.HasIndex(item => new { item.CreatedByUserId, item.CreatedAtUtc });
        builder.HasOne<Appointment>().WithMany().HasForeignKey(item => item.AppointmentId).OnDelete(DeleteBehavior.Cascade);
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
