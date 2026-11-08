using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class ClinicStaffMemberConfiguration : IEntityTypeConfiguration<ClinicStaffMember>
{
    public void Configure(EntityTypeBuilder<ClinicStaffMember> builder)
    {
        builder.ToTable("ClinicStaffMembers");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Role).HasMaxLength(32);
        builder.HasIndex(item => new { item.ClinicId, item.UserId }).IsUnique();
        builder.HasIndex(item => item.UserId);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClinicQueueSettingsConfiguration : IEntityTypeConfiguration<ClinicQueueSettings>
{
    public void Configure(EntityTypeBuilder<ClinicQueueSettings> builder)
    {
        builder.ToTable("ClinicQueueSettings");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TokenPrefix).HasMaxLength(4);
        builder.HasIndex(item => new { item.ClinicId, item.BranchId }).IsUnique();
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClinicQueueSessionConfiguration : IEntityTypeConfiguration<ClinicQueueSession>
{
    public void Configure(EntityTypeBuilder<ClinicQueueSession> builder)
    {
        builder.ToTable("ClinicQueueSessions");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.ClinicId, item.BranchId, item.DoctorId, item.OperatingDate }).IsUnique();
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QueueTokenCounterConfiguration : IEntityTypeConfiguration<QueueTokenCounter>
{
    public void Configure(EntityTypeBuilder<QueueTokenCounter> builder)
    {
        builder.ToTable("QueueTokenCounters");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.RangeStartMinutes).HasDefaultValue(QueueTokenCounter.DayWideRangeMinutes);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.ClinicId, item.BranchId, item.DoctorId, item.OperatingDate, item.RangeStartMinutes })
            .IsUnique()
            .HasDatabaseName("IX_QueueTokenCounters_Clinic_Branch_Doctor_Date_Range");
    }
}

public sealed class QueueEntryConfiguration : IEntityTypeConfiguration<QueueEntry>
{
    public void Configure(EntityTypeBuilder<QueueEntry> builder)
    {
        builder.ToTable("QueueEntries");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.DisplayName).HasMaxLength(80);
        builder.Property(item => item.TokenLabel).HasMaxLength(16);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.ClinicId, item.BranchId, item.DoctorId, item.OperatingDate, item.TokenNumber }).IsUnique();
        builder.HasIndex(item => item.AppointmentId)
            .IsUnique()
            .HasFilter("[AppointmentId] IS NOT NULL AND [Status] IN (1, 2, 3, 7)");
        builder.HasIndex(item => new { item.DoctorId, item.BranchId, item.OperatingDate, item.Status });
        builder.HasIndex(item => item.AccountUserId);
        builder.HasOne<ClinicQueueSession>().WithMany().HasForeignKey(item => item.QueueSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(item => item.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WalkInRecord>().WithMany().HasForeignKey(item => item.WalkInRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QueueStatusHistoryConfiguration : IEntityTypeConfiguration<QueueStatusHistory>
{
    public void Configure(EntityTypeBuilder<QueueStatusHistory> builder)
    {
        builder.ToTable("QueueStatusHistory");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasMaxLength(120);
        builder.HasIndex(item => item.QueueEntryId);
        builder.HasOne<QueueEntry>().WithMany().HasForeignKey(item => item.QueueEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppointmentCheckInTokenConfiguration : IEntityTypeConfiguration<AppointmentCheckInToken>
{
    public void Configure(EntityTypeBuilder<AppointmentCheckInToken> builder)
    {
        builder.ToTable("AppointmentCheckInTokens");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TokenHash).HasMaxLength(64);
        builder.HasIndex(item => item.TokenHash).IsUnique();
        builder.HasIndex(item => item.AppointmentId);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(item => item.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WalkInRecordConfiguration : IEntityTypeConfiguration<WalkInRecord>
{
    public void Configure(EntityTypeBuilder<WalkInRecord> builder)
    {
        builder.ToTable("WalkInRecords");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.PatientDisplayName).HasMaxLength(80);
        builder.Property(item => item.ContactNumber).HasMaxLength(20);
        builder.Property(item => item.ReasonForVisit).HasMaxLength(120);
        builder.Property(item => item.ConsultationFee).HasPrecision(10, 2);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.HasIndex(item => new { item.ClinicId, item.BranchId, item.CreatedAtUtc });
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(64);
        builder.Property(item => item.Title).HasMaxLength(120);
        builder.Property(item => item.Body).HasMaxLength(500);
        builder.HasIndex(item => new { item.UserId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.UserId, item.Kind, item.SubjectId, item.CreatedAtUtc });
    }
}
