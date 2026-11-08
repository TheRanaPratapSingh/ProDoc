using Microsoft.EntityFrameworkCore;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Admin;

/// <summary>
/// Ensures Super-Admin provisioned doctors have a published clinic schedule so patient discovery works.
/// </summary>
internal static class DoctorPublicDiscoveryHelper
{
    public static async Task EnsurePublishedClinicScheduleAsync(
        ProDocDbContext db,
        Guid doctorId,
        Guid clinicId,
        Guid? preferredBranchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.ConsultationSchedules.SingleOrDefaultAsync(
            item => item.DoctorId == doctorId && item.ClinicId == clinicId && item.IsPublished,
            cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var clinic = await db.Clinics
            .Include(item => item.Branches)
            .SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken)
            ?? throw DomainException.NotFound(
                "clinic_not_found",
                "That clinic was not found. Assign an approved clinic before activating the doctor.");

        if (clinic.Status != VerificationStatus.Approved)
        {
            throw DomainException.Validation(
                "clinic_not_approved",
                "Assign an approved clinic so the doctor can appear in patient search.");
        }

        var branch = clinic.Branches.FirstOrDefault(item =>
                preferredBranchId is Guid selected && item.Id == selected && item.IsActive)
            ?? clinic.Branches.FirstOrDefault(item => item.IsPrimary && item.IsActive)
            ?? clinic.Branches.FirstOrDefault(item => item.IsActive)
            ?? throw DomainException.Validation(
                "clinic_branch_required",
                "The clinic needs an active branch before the doctor can appear in patient search.");

        var draft = await db.ConsultationSchedules.SingleOrDefaultAsync(
            item => item.DoctorId == doctorId && item.ClinicId == clinicId && item.BranchId == branch.Id,
            cancellationToken);

        if (draft is null)
        {
            draft = ConsultationSchedule.Create(
                doctorId,
                clinicId,
                branch.Id,
                consultationFee: 500m,
                slotDurationMinutes: 15,
                now);
            ApplyDefaultHours(draft, now);
            db.ConsultationSchedules.Add(draft);
        }
        else if (draft.Hours.Count == 0)
        {
            ApplyDefaultHours(draft, now);
        }

        if (!draft.IsPublished)
        {
            draft.Publish(now);
        }
    }

    public static async Task EnsureDiscoverableAsync(
        ProDocDbContext db,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var hasPlace = await (
            from schedule in db.ConsultationSchedules.AsNoTracking()
            where schedule.DoctorId == doctorId && schedule.IsPublished
            join clinic in db.Clinics.AsNoTracking() on schedule.ClinicId equals clinic.Id
            join branch in db.ClinicBranches.AsNoTracking() on schedule.BranchId equals branch.Id
            where clinic.Status == VerificationStatus.Approved
                && branch.IsActive
                && db.DoctorClinicAffiliations.Any(link => link.DoctorId == doctorId && link.ClinicId == clinic.Id)
            select schedule.Id).AnyAsync(cancellationToken);

        if (!hasPlace)
        {
            throw DomainException.Validation(
                "doctor_not_discoverable",
                "The doctor was approved but could not be published for patient search. Check clinic affiliation and active branches.");
        }
    }

    private static void ApplyDefaultHours(ConsultationSchedule schedule, DateTimeOffset now) =>
        schedule.ApplyDefaultWeekdayHours(now);
}
