using ProDoc.Application.Abstractions;
using ProDoc.Application.Appointments;
using ProDoc.Application.Billing;
using ProDoc.Application.Clinical;
using ProDoc.Application.Queue;
using ProDoc.Application.Reviews;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public sealed class DoctorPortalService(
    IProviderStore providers,
    BookingService booking,
    FinanceService finance,
    ReviewService reviews,
    IMedicalStore medical,
    QueueService queues,
    ScheduleService schedules,
    IClock clock)
{
    public async Task<DoctorDashboard> GetDashboardAsync(Guid userId, Guid? clinicId, CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var clinics = await ListClinicOptionsAsync(doctor.Id, cancellationToken);
        var selected = ResolveClinic(clinics, clinicId);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var from = today.AddDays(-7);
        var to = today.AddDays(21);

        var practice = await ListPracticeWindowsAsync(
            userId,
            selected?.ClinicId,
            from,
            to,
            cancellationToken);

        var todayRows = practice.Where(item => LocalDate(item) == today).ToArray();
        var upcoming = practice.Count(item =>
            LocalDate(item) > today
            && item.Status is nameof(AppointmentStatus.Confirmed) or nameof(AppointmentStatus.Requested) or nameof(AppointmentStatus.CheckedIn));
        var completed = practice.Count(item => item.Status == nameof(AppointmentStatus.Completed) && LocalDate(item) == today);
        var pending = practice.Count(item =>
            item.Status is nameof(AppointmentStatus.Requested) or nameof(AppointmentStatus.Confirmed)
            && LocalDate(item) == today);
        var cancelled = practice.Count(item =>
            item.Status is nameof(AppointmentStatus.Cancelled) or nameof(AppointmentStatus.Rejected)
            && LocalDate(item) == today);
        var waiting = practice.Count(item =>
            item.Status == nameof(AppointmentStatus.CheckedIn) && LocalDate(item) == today);
        var patients = practice
            .Where(item => item.Status is not nameof(AppointmentStatus.Cancelled) and not nameof(AppointmentStatus.Rejected))
            .Select(item => $"{item.PatientName}|{item.FamilyMemberId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var consult = await medical.CountDoctorActivityAsync(doctor.Id, today, selected?.ClinicId, cancellationToken);
        var earnings = await finance.DoctorEarningsAsync(userId, selected?.ClinicId, cancellationToken);
        var rating = await reviews.DoctorRatingSummaryAsync(doctor.Id, cancellationToken);
        var notifications = await queues.ListNotificationsAsync(userId, cancellationToken);
        var unread = notifications.Count(item => !item.IsRead);
        var specialization = await ResolveSpecializationNameAsync(doctor, cancellationToken);
        var availability = await BuildAvailabilityAsync(userId, doctor.Id, selected?.ClinicId, today, cancellationToken);

        var preview = todayRows
            .Where(item => item.Status is not nameof(AppointmentStatus.Cancelled) and not nameof(AppointmentStatus.Rejected))
            .OrderBy(item => item.StartsAtUtc)
            .Take(8)
            .ToArray();
        var next = preview.FirstOrDefault(item =>
            item.Status is nameof(AppointmentStatus.CheckedIn)
                or nameof(AppointmentStatus.Confirmed)
                or nameof(AppointmentStatus.Requested)
                or nameof(AppointmentStatus.Held))
            ?? preview.FirstOrDefault();

        var display = !string.IsNullOrWhiteSpace(doctor.DisplayName)
            ? doctor.DisplayName!
            : $"{doctor.FirstName} {doctor.LastName}".Trim();
        return new DoctorDashboard(
            doctor.Id,
            string.IsNullOrWhiteSpace(display) ? "Doctor" : display,
            specialization,
            DoctorProfileCompletion.PhotoDocumentId(doctor),
            DoctorProfileCompletion.VerificationBadge(doctor.Status),
            doctor.Status.ToString(),
            DoctorProfileCompletion.Percent(doctor, today),
            selected?.ClinicId,
            selected?.Name,
            unread,
            new DoctorAppointmentCounts(
                todayRows.Length,
                upcoming,
                completed,
                pending,
                cancelled,
                waiting,
                consult.InProgress,
                patients),
            new DoctorConsultationSummary(consult.InProgress, consult.CompletedToday, consult.WithPrescriptionToday),
            new DoctorEarningsSummary(
                earnings.PaymentsEnabled,
                earnings.TotalGross,
                earnings.TodayGross,
                earnings.MonthGross,
                earnings.CommissionDeducted,
                earnings.NetPayable,
                earnings.PendingSettlement),
            availability,
            rating,
            clinics,
            preview,
            next);
    }

    public async Task<DoctorEarningsDashboard> GetEarningsAsync(Guid userId, Guid? clinicId, CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var clinics = await ListClinicOptionsAsync(doctor.Id, cancellationToken);
        var selected = ResolveClinic(clinics, clinicId);
        return await finance.DoctorEarningsAsync(userId, selected?.ClinicId, cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorClinicOption>> ListClinicsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        return await ListClinicOptionsAsync(doctor.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorPatientRow>> ListPatientsAsync(
        Guid userId,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var clinics = await ListClinicOptionsAsync(doctor.Id, cancellationToken);
        var selected = ResolveClinic(clinics, clinicId);
        var to = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var from = to.AddDays(-180);
        var practice = await ListPracticeWindowsAsync(userId, selected?.ClinicId, from, to, cancellationToken);
        return practice
            .GroupBy(item => $"{item.PatientName}|{item.FamilyMemberId}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var last = group.OrderByDescending(item => item.StartsAtUtc).First();
                return new DoctorPatientRow(
                    last.AccountUserId,
                    last.PatientName,
                    group.Count(),
                    last.StartsAtUtc,
                    last.Status,
                    last.Id);
            })
            .OrderByDescending(item => item.LastVisitAtUtc)
            .ToArray();
    }

    public async Task<IReadOnlyList<DoctorPrescriptionRow>> ListPrescriptionsAsync(
        Guid userId,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var clinics = await ListClinicOptionsAsync(doctor.Id, cancellationToken);
        var selected = ResolveClinic(clinics, clinicId);
        return await medical.ListDoctorPrescriptionsAsync(doctor.Id, selected?.ClinicId, cancellationToken);
    }

    public Task<IReadOnlyList<NotificationDetails>> ListNotificationsAsync(Guid userId, CancellationToken cancellationToken) =>
        queues.ListNotificationsAsync(userId, cancellationToken);

    private async Task<IReadOnlyList<AppointmentDetails>> ListPracticeWindowsAsync(
        Guid userId,
        Guid? clinicId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to.DayNumber - from.DayNumber <= 31)
        {
            return await booking.ListPracticeAsync(userId, from, to, cancellationToken, clinicId, assignedOnly: true);
        }

        var rows = new List<AppointmentDetails>();
        var cursor = from;
        while (cursor <= to)
        {
            var end = DateOnly.FromDayNumber(Math.Min(to.DayNumber, cursor.DayNumber + 31));
            rows.AddRange(await booking.ListPracticeAsync(userId, cursor, end, cancellationToken, clinicId, assignedOnly: true));
            if (end >= to)
            {
                break;
            }

            cursor = end.AddDays(1);
        }

        return rows
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.StartsAtUtc)
            .ToArray();
    }

    private async Task<DoctorAvailabilitySummary?> BuildAvailabilityAsync(
        Guid userId,
        Guid doctorId,
        Guid? clinicId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var mine = await schedules.ListMineAsync(userId, cancellationToken);
        var schedule = mine
            .Where(item => item.DoctorId == doctorId)
            .Where(item => clinicId is null || item.ClinicId == clinicId)
            .OrderByDescending(item => item.IsPublished)
            .ThenByDescending(item => item.ClinicId == clinicId)
            .FirstOrDefault();
        if (schedule is null)
        {
            return null;
        }

        var closure = schedule.Closures.FirstOrDefault(item => item.Date == today);
        var weeklyHours = schedule.Hours.Where(item => item.Day == today.DayOfWeek).OrderBy(item => item.StartsAt).ToArray();
        var extras = schedule.Extras.Where(item => item.Date == today).OrderBy(item => item.StartsAt).ToArray();
        // Date-specific extras replace the weekly template for that day.
        var dayWindows = extras.Length > 0
            ? extras.Select(item => (item.StartsAt, item.EndsAt)).ToArray()
            : weeklyHours.Select(item => (item.StartsAt, item.EndsAt)).ToArray();
        var timingsLabel = dayWindows.Length == 0
            ? null
            : string.Join(
                " | ",
                dayWindows.Select(item => $"{FormatClock(item.StartsAt)} – {FormatClock(item.EndsAt)}"));
        TimeOnly? breakStart = null;
        TimeOnly? breakEnd = null;

        if (closure is not null)
        {
            return new DoctorAvailabilitySummary(
                false,
                "Unavailable",
                dayWindows.Length > 0 ? dayWindows[0].StartsAt.ToString("HH:mm") : null,
                dayWindows.Length > 0 ? dayWindows[^1].EndsAt.ToString("HH:mm") : null,
                breakStart?.ToString("HH:mm"),
                breakEnd?.ToString("HH:mm"),
                closure.Reason,
                schedule.Id,
                schedule.SlotDurationMinutes,
                closure.Id,
                timingsLabel);
        }

        if (dayWindows.Length == 0)
        {
            return new DoctorAvailabilitySummary(
                false,
                "Unavailable",
                null,
                null,
                null,
                null,
                "Non-working day",
                schedule.Id,
                schedule.SlotDurationMinutes,
                null,
                null);
        }

        var first = dayWindows[0];
        var last = dayWindows[^1];
        return new DoctorAvailabilitySummary(
            schedule.IsPublished,
            schedule.IsPublished ? "Available" : "Draft",
            first.StartsAt.ToString("HH:mm"),
            last.EndsAt.ToString("HH:mm"),
            breakStart?.ToString("HH:mm"),
            breakEnd?.ToString("HH:mm"),
            null,
            schedule.Id,
            schedule.SlotDurationMinutes,
            null,
            timingsLabel);
    }

    private static string FormatClock(TimeOnly value) =>
        value.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<string?> ResolveSpecializationNameAsync(Doctor doctor, CancellationToken cancellationToken)
    {
        var primary = doctor.Specializations.FirstOrDefault(item => item.IsPrimary)
            ?? doctor.Specializations.FirstOrDefault();
        if (primary is null)
        {
            return null;
        }

        var catalog = await providers.ListSpecializationsAsync(cancellationToken);
        return catalog.FirstOrDefault(item => item.Id == primary.SpecializationId)?.Name;
    }

    private async Task<IReadOnlyList<DoctorClinicOption>> ListClinicOptionsAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var affiliations = await providers.ListAffiliationDetailsAsync(doctorId, approvedClinicsOnly: false, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken);
        return affiliations
            .Select(item => new DoctorClinicOption(
                item.ClinicId,
                item.ClinicName,
                item.ClinicStatus,
                item.BranchId,
                item.BranchName,
                item.Kind,
                item.IsPrimary))
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Doctor> RequireDoctorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "Start a doctor profile first.");
        return doctor;
    }

    private static DoctorClinicOption? ResolveClinic(IReadOnlyList<DoctorClinicOption> clinics, Guid? clinicId)
    {
        if (clinicId is null)
        {
            return null;
        }

        // Stale client selection (other account / removed affiliation) → treat as All Clinics.
        return clinics.FirstOrDefault(item => item.ClinicId == clinicId.Value);
    }

    private static DateOnly LocalDate(AppointmentDetails item)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(item.TimeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartsAtUtc, zone).DateTime);
    }
}
