using ProDoc.Application.Abstractions;
using ProDoc.Application.Appointments;
using ProDoc.Application.Billing;
using ProDoc.Application.Clinical;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Reception;

public sealed class ReceptionPortalService(
    IClinicAccess access,
    IProviderStore providers,
    QueueService queues,
    BookingService booking,
    FinanceService finance,
    IMedicalStore medical,
    IAuthStore auth,
    IPlaceLinkBuilder placeLinks,
    IClock clock)
{
    public async Task<ReceptionDashboard> GetDashboardAsync(
        Guid userId,
        Guid clinicId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        var branch = ResolveBranch(clinic, branchId);
        var today = LocalToday();
        var practice = await booking.ListPracticeAsync(userId, today, today, cancellationToken, clinicId);
        if (branch is not null)
        {
            practice = practice.Where(item => item.BranchId == branch.Id).ToArray();
        }

        var assigned = await access.ListAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
        if (assigned.Count > 0)
        {
            var allowed = assigned.ToHashSet();
            practice = practice.Where(item => allowed.Contains(item.DoctorId)).ToArray();
        }

        IReadOnlyList<QueueEntryDetails> queueRows = [];
        if (branch is not null)
        {
            queueRows = await queues.ListBranchQueueAsync(userId, clinicId, branch.Id, today, cancellationToken);
            if (assigned.Count > 0)
            {
                var allowed = assigned.ToHashSet();
                queueRows = queueRows.Where(item => allowed.Contains(item.DoctorId)).ToArray();
            }
        }

        var doctors = branch is null
            ? []
            : await queues.ListDeskDoctorsAsync(userId, clinicId, branch.Id, cancellationToken);

        var doctorOverview = doctors.Select(doctor =>
        {
            var rows = practice.Where(item => item.DoctorId == doctor.Id).ToArray();
            var q = queueRows.Where(item => item.DoctorId == doctor.Id).ToArray();
            return new DeskDoctorOverview(
                doctor.Id,
                doctor.DisplayName,
                rows.Length,
                q.Count(item => item.Status == nameof(QueueEntryStatus.Waiting)),
                q.Count(item => item.Status == nameof(QueueEntryStatus.InConsultation)),
                rows.Where(item => item.Status is nameof(AppointmentStatus.Confirmed) or nameof(AppointmentStatus.CheckedIn) or nameof(AppointmentStatus.Requested))
                    .OrderBy(item => item.StartsAtUtc)
                    .FirstOrDefault());
        }).ToArray();

        var pendingPay = practice.Where(item =>
            item.Status is not nameof(AppointmentStatus.Cancelled) and not nameof(AppointmentStatus.Rejected)
            && !string.Equals(item.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).ToArray();
        var paidToday = practice.Where(item => string.Equals(item.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).ToArray();

        var timeline = practice.OrderBy(item => item.StartsAtUtc).Take(20).ToArray();
        var next = practice
            .Where(item => item.Status is nameof(AppointmentStatus.Confirmed) or nameof(AppointmentStatus.CheckedIn) or nameof(AppointmentStatus.Requested))
            .OrderBy(item => item.StartsAtUtc)
            .FirstOrDefault();

        var notifications = (await queues.ListNotificationsAsync(userId, cancellationToken)).Take(10).ToArray();
        var hourly = Enumerable.Range(8, 12).Select(hour =>
        {
            var count = practice.Count(item =>
            {
                var local = TimeZoneInfo.ConvertTime(item.StartsAtUtc, Zone(item.TimeZoneId)).DateTime;
                return local.Hour == hour;
            });
            return new ReceptionChartPoint($"{hour:00}:00", count);
        }).ToArray();

        return new ReceptionDashboard(
            clinic.Id,
            clinic.Name ?? clinic.Reference,
            branch?.Id,
            branch?.Name,
            new ReceptionAppointmentCounts(
                practice.Count,
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.Waiting)),
                practice.Count(item => item.Status == nameof(AppointmentStatus.CheckedIn)),
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.InConsultation)),
                practice.Count(item => item.Status == nameof(AppointmentStatus.Completed)),
                practice.Count(item => item.Status is nameof(AppointmentStatus.Cancelled) or nameof(AppointmentStatus.Rejected)),
                0,
                practice.Count(item => item.IsWalkIn)),
            new ReceptionQueueCounts(
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.Waiting)),
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.Called)),
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.InConsultation)),
                queueRows.Count(item => item.Status == nameof(QueueEntryStatus.Completed)),
                queueRows.Count(item => item.IsWalkIn)),
            new ReceptionPaymentCounts(
                pendingPay.Length,
                paidToday.Length,
                pendingPay.Sum(item => item.ConsultationFee),
                paidToday.Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.ConsultationFee)),
            doctorOverview,
            timeline,
            next,
            notifications,
            hourly);
    }

    public async Task<IReadOnlyList<ReceptionPatientRow>> SearchPatientsAsync(
        Guid userId,
        Guid clinicId,
        Guid? branchId,
        string? query,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);

        var needle = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        Guid? accountUserId = null;
        string? nameQuery = needle;
        if (needle is not null && PhoneNumber.TryParseIndianMobile(needle, out var phone))
        {
            var account = await auth.FindAccountByPhoneAsync(phone.E164, cancellationToken);
            if (account is null)
            {
                return [];
            }

            accountUserId = account.UserId;
            nameQuery = null;
        }

        var visits = await booking.SearchClinicPatientVisitsAsync(
            userId,
            clinicId,
            branchId,
            nameQuery,
            accountUserId,
            cancellationToken);

        return visits
            .GroupBy(item => item.PatientName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var last = group.OrderByDescending(item => item.StartsAtUtc).First();
                return new ReceptionPatientRow(
                    null,
                    last.PatientName,
                    null,
                    group.Count(),
                    last.StartsAtUtc,
                    last.Status,
                    last.Id,
                    last.DoctorId,
                    last.DoctorName);
            })
            .OrderByDescending(item => item.LastVisitAtUtc)
            .Take(50)
            .ToArray();
    }

    public async Task<ReceptionPatientProfile> RegisterOrUpdatePatientAsync(
        Guid staffUserId,
        Guid clinicId,
        string fullName,
        string phone,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(staffUserId, clinicId, cancellationToken);
        var e164 = PhoneNumber.ParseIndianMobile(phone).E164;
        var now = clock.UtcNow;
        var account = await auth.FindAccountByPhoneAsync(e164, cancellationToken);
        if (account is null)
        {
            account = await auth.CreatePatientAccountAsync(e164, [RoleNames.Patient], now, cancellationToken);
        }

        await auth.UpdatePatientProfileAsync(account.UserId, fullName.Trim(), Languages.English, now, cancellationToken);
        await auth.SaveChangesAsync(cancellationToken);

        var history = (await booking.SearchClinicPatientVisitsAsync(
                staffUserId,
                clinicId,
                branchId: null,
                fullName.Trim(),
                account.UserId,
                cancellationToken))
            .OrderByDescending(item => item.StartsAtUtc)
            .Take(20)
            .ToArray();

        return new ReceptionPatientProfile(
            account.UserId,
            fullName.Trim(),
            MaskPhone(e164),
            history);
    }

    public async Task<ReceptionClinicInfo> GetClinicInfoAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        var branches = clinic.Branches
            .Where(item => item.IsActive)
            .Select(item =>
            {
                var link = new PlaceLinkInput(
                    item.Name,
                    item.FormattedAddress,
                    item.Latitude,
                    item.Longitude,
                    item.GooglePlaceId);
                return new ReceptionBranchInfo(
                    item.Id,
                    item.Name,
                    item.AddressLine,
                    item.AddressLine2,
                    item.Landmark,
                    item.Locality,
                    item.City,
                    item.District,
                    item.State,
                    item.PostalCode,
                    item.Country,
                    item.Latitude,
                    item.Longitude,
                    item.GooglePlaceId,
                    item.FormattedAddress,
                    placeLinks.MapsUrl(link),
                    placeLinks.DirectionsUrl(link),
                    item.Latitude is not null && item.Longitude is not null,
                    item.IsPrimary,
                    item.IsActive);
            })
            .ToArray();
        var primary = branches.FirstOrDefault(item => item.IsPrimary) ?? branches.FirstOrDefault();
        var doctors = primary is null
            ? Array.Empty<DeskDoctor>()
            : await queues.ListDeskDoctorsAsync(userId, clinicId, primary.Id, cancellationToken);
        return new ReceptionClinicInfo(
            clinic.Id,
            clinic.Name ?? clinic.Reference,
            clinic.PhoneNumber,
            clinic.Type?.ToString(),
            branches,
            doctors);
    }

    public async Task<ReceptionReportSummary> GetReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);
        if (to < from || to.DayNumber - from.DayNumber > 92)
        {
            throw DomainException.Validation("date_invalid", "Choose a range of 92 days or fewer.");
        }

        var practice = await ListPracticeWindowsAsync(userId, clinicId, from, to, cancellationToken);
        var byDoctor = practice
            .GroupBy(item => item.DoctorId)
            .Select(group =>
            {
                var sample = group.First();
                return new ReceptionDoctorReportRow(
                    group.Key,
                    sample.DoctorName,
                    group.Count(),
                    group.Count(item => item.IsWalkIn),
                    0,
                    group.Count(item => item.Status == nameof(AppointmentStatus.Completed)),
                    group.Where(item => string.Equals(item.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                        .Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.ConsultationFee));
            })
            .OrderByDescending(item => item.Appointments)
            .ToArray();

        return new ReceptionReportSummary(
            clinicId,
            from,
            to,
            practice.Count,
            practice.Count(item => item.IsWalkIn),
            0,
            practice.Count(item => item.Status == nameof(AppointmentStatus.Completed)),
            practice.Count(item => item.Status is nameof(AppointmentStatus.Cancelled) or nameof(AppointmentStatus.Rejected)),
            practice.Where(item => string.Equals(item.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.ConsultationFee),
            byDoctor);
    }

    public async Task<IReadOnlyList<ReceptionDeskPaymentRow>> ListPendingPaymentsAsync(
        Guid userId,
        Guid clinicId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);
        var today = LocalToday();
        var practice = await booking.ListPracticeAsync(userId, today.AddDays(-7), today, cancellationToken, clinicId);
        if (branchId is Guid branch)
        {
            practice = practice.Where(item => item.BranchId == branch).ToArray();
        }

        return practice
            .Where(item =>
                item.Status is not nameof(AppointmentStatus.Cancelled) and not nameof(AppointmentStatus.Rejected)
                && !string.Equals(item.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.StartsAtUtc)
            .Select(item => new ReceptionDeskPaymentRow(
                item.Id,
                item.Reference ?? item.Id.ToString("N")[..8],
                item.PatientName,
                item.DoctorName,
                item.ConsultationFee,
                item.Currency,
                item.PaymentStatus ?? "Unpaid",
                item.StartsAtUtc))
            .ToArray();
    }

    public async Task<PaymentDetails> CollectPaymentAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken) =>
        await finance.RecordClinicCollectionAsync(userId, appointmentId, metadata, cancellationToken);

    public async Task<ReceptionRecordsStatus> GetRecordsStatusAsync(
        Guid userId,
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var appointment = await medical.FindAppointmentAsync(appointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        await access.EnsureDoctorCoverageAsync(userId, appointment.ClinicId, appointment.BranchId, appointment.DoctorId, cancellationToken);

        var consultation = await medical.FindActiveConsultationForAppointmentAsync(appointmentId, cancellationToken);
        var record = await medical.FindRecordAsync(appointment.AccountUserId, appointment.FamilyMemberId, false, cancellationToken);
        var prescriptions = record is null
            ? []
            : await medical.ListPrescriptionsAsync(record.Id, cancellationToken);
        var forVisit = prescriptions.Where(item => item.AppointmentId == appointmentId).ToArray();
        var latest = forVisit.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault();

        return new ReceptionRecordsStatus(
            appointmentId,
            consultation is not null,
            consultation?.Status.ToString(),
            latest is not null
                && latest.Status is Domain.Clinical.PrescriptionStatus.Finalized or Domain.Clinical.PrescriptionStatus.Amended,
            latest?.Status.ToString(),
            forVisit.Length);
    }

    public Task<IReadOnlyList<NotificationDetails>> ListNotificationsAsync(Guid userId, CancellationToken cancellationToken) =>
        queues.ListNotificationsAsync(userId, cancellationToken);

    private async Task<IReadOnlyList<AppointmentDetails>> ListPracticeWindowsAsync(
        Guid userId,
        Guid clinicId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to.DayNumber - from.DayNumber <= 31)
        {
            return await booking.ListPracticeAsync(userId, from, to, cancellationToken, clinicId);
        }

        var rows = new List<AppointmentDetails>();
        var cursor = from;
        while (cursor <= to)
        {
            var end = DateOnly.FromDayNumber(Math.Min(to.DayNumber, cursor.DayNumber + 31));
            rows.AddRange(await booking.ListPracticeAsync(userId, cursor, end, cancellationToken, clinicId));
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

    private static ClinicBranch? ResolveBranch(Clinic clinic, Guid? branchId)
    {
        if (branchId is Guid id)
        {
            return clinic.Branches.FirstOrDefault(item => item.Id == id && item.IsActive)
                ?? throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        }

        return clinic.Branches.FirstOrDefault(item => item.IsActive && item.IsPrimary)
            ?? clinic.Branches.FirstOrDefault(item => item.IsActive);
    }

    private DateOnly LocalToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Zone(ConsultationSchedule.DefaultTimeZone)).DateTime);

    private static TimeZoneInfo Zone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string MaskPhone(string e164) =>
        e164.Length >= 4 ? string.Concat("+91******", e164.AsSpan(e164.Length - 4)) : "******";
}
