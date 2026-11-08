using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class ConsultationScheduleConfiguration : IEntityTypeConfiguration<ConsultationSchedule>
{
    public void Configure(EntityTypeBuilder<ConsultationSchedule> builder)
    {
        builder.ToTable("ConsultationSchedules");
        builder.Property(schedule => schedule.Id).ValueGeneratedNever();
        builder.Property(schedule => schedule.ConsultationFee).HasPrecision(10, 2);
        builder.Property(schedule => schedule.TimeZoneId).HasMaxLength(64).HasDefaultValue(ConsultationSchedule.DefaultTimeZone);
        builder.Property(schedule => schedule.BufferMinutes).HasDefaultValue(0);
        builder.Property(schedule => schedule.RequiresApproval).HasDefaultValue(false);
        builder.Property(schedule => schedule.BookingMode).HasDefaultValue(ScheduleBookingMode.TimedSlots);
        builder.Property(schedule => schedule.RowVersion).IsRowVersion();
        builder.HasIndex(schedule => schedule.DoctorId).IsUnique().HasFilter("[ClinicId] IS NULL");
        builder.HasIndex(schedule => new { schedule.DoctorId, schedule.ClinicId, schedule.BranchId })
            .IsUnique()
            .HasFilter("[ClinicId] IS NOT NULL");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(schedule => schedule.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(schedule => schedule.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(schedule => schedule.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(schedule => schedule.Hours).WithOne().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(schedule => schedule.Breaks).WithOne().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(schedule => schedule.Closures).WithOne().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(schedule => schedule.Extras).WithOne().HasForeignKey(item => item.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(schedule => schedule.Hours).HasField("_hours").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(schedule => schedule.Breaks).HasField("_breaks").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(schedule => schedule.Closures).HasField("_closures").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(schedule => schedule.Extras).HasField("_extras").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class WeeklyHoursConfiguration : IEntityTypeConfiguration<WeeklyHours>
{
    public void Configure(EntityTypeBuilder<WeeklyHours> builder)
    {
        builder.ToTable("WeeklyHours");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Day).HasConversion<int>();
    }
}

public sealed class ScheduleBreakConfiguration : IEntityTypeConfiguration<ScheduleBreak>
{
    public void Configure(EntityTypeBuilder<ScheduleBreak> builder)
    {
        builder.ToTable("ScheduleBreaks");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Day).HasConversion<int>();
    }
}

public sealed class ScheduleClosureConfiguration : IEntityTypeConfiguration<ScheduleClosure>
{
    public void Configure(EntityTypeBuilder<ScheduleClosure> builder)
    {
        builder.ToTable("ScheduleClosures");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasMaxLength(120);
        builder.HasIndex(item => new { item.ScheduleId, item.Date }).IsUnique();
    }
}

public sealed class ScheduleExtraConfiguration : IEntityTypeConfiguration<ScheduleExtra>
{
    public void Configure(EntityTypeBuilder<ScheduleExtra> builder)
    {
        builder.ToTable("ScheduleExtras");
        builder.Property(item => item.Id).ValueGeneratedNever();
    }
}

public sealed class BranchOperatingHourConfiguration : IEntityTypeConfiguration<BranchOperatingHour>
{
    public void Configure(EntityTypeBuilder<BranchOperatingHour> builder)
    {
        builder.ToTable("BranchOperatingHours");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Day).HasConversion<int>();
        builder.HasIndex(item => item.BranchId);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BranchClosureConfiguration : IEntityTypeConfiguration<BranchClosure>
{
    public void Configure(EntityTypeBuilder<BranchClosure> builder)
    {
        builder.ToTable("BranchClosures");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasMaxLength(120);
        builder.HasIndex(item => new { item.BranchId, item.Date }).IsUnique();
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClinicFacilityConfiguration : IEntityTypeConfiguration<ClinicFacility>
{
    public void Configure(EntityTypeBuilder<ClinicFacility> builder)
    {
        builder.ToTable("ClinicFacilities");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(item => new { item.ClinicId, item.Name }).IsUnique();
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DoctorFavouriteConfiguration : IEntityTypeConfiguration<DoctorFavourite>
{
    public void Configure(EntityTypeBuilder<DoctorFavourite> builder)
    {
        builder.ToTable("DoctorFavourites");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.UserId, item.DoctorId }).IsUnique();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}
