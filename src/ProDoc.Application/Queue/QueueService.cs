using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Billing;
using ProDoc.Application.Growth;
using ProDoc.Application.Mobile;
using ProDoc.Application.Providers;
using ProDoc.Application.Reviews;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Queue;

public sealed class QueueService(
    IQueueStore queue,
    IClinicAccess access,
    IProviderStore providers,
    IScheduleStore schedules,
    IQueueRealtime realtime,
    FinanceService finance,
    ReviewService reviews,
    GrowthService growth,
    MobileDeviceService mobileDevices,
    IAutomationEventBus automation,
    IClock clock,
    ILogger<QueueService> logger)
{
    public async Task<IReadOnlyList<DeskClinic>> ListDeskClinicsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        var staff = await queue.ListStaffForUserAsync(userId, cancellationToken);
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        var affiliations = doctor is null
            ? []
            : await providers.ListAffiliationsAsync(doctor.Id, cancellationToken);
        var ids = owned.Select(item => item.Id)
            .Concat(staff.Select(item => item.ClinicId))
            .Concat(affiliations.Select(item => item.ClinicId))
            .Distinct()
            .ToArray();
        var desks = new List<DeskClinic>();
        foreach (var clinicId in ids)
        {
            var clinic = owned.FirstOrDefault(item => item.Id == clinicId) ?? await queue.FindClinicAsync(clinicId, cancellationToken);
            if (clinic is null)
            {
                continue;
            }

            var allowedBranches = staff.Where(item => item.ClinicId == clinicId).Select(item => item.BranchId).ToArray();
            var affiliatedBranches = affiliations.Where(item => item.ClinicId == clinicId).Select(item => item.BranchId).ToArray();
            var owner = owned.Any(item => item.Id == clinicId);
            var branches = clinic.Branches
                .Where(branch => branch.IsActive)
                .Where(branch =>
                    owner
                    || allowedBranches.Any(item => item is null || item == branch.Id)
                    || affiliatedBranches.Any(item => item is null || item == branch.Id))
                .Select(branch => new DeskBranch(branch.Id, branch.Name, branch.IsActive))
                .ToArray();
            desks.Add(new DeskClinic(clinic.Id, clinic.Name ?? clinic.Reference, branches));
        }

        return desks;
    }

    public async Task<IReadOnlyList<DeskDoctor>> ListDeskDoctorsAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await access.EnsureQueueAccessAsync(userId, clinicId, branchId, cancellationToken);
        var rows = await queue.ListClinicDoctorsAsync(clinicId, branchId, cancellationToken);
        var assigned = await queue.ListReceptionistAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
        if (assigned.Count > 0)
        {
            var allowed = assigned.ToHashSet();
            rows = rows.Where(row => allowed.Contains(row.DoctorId)).ToArray();
        }

        return rows.Select(row => new DeskDoctor(row.DoctorId, row.DisplayName, row.YearsOfExperience)).ToArray();
    }

    public async Task<QueueSettingsDetails> GetSettingsAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        await access.EnsureQueueAccessAsync(userId, clinicId, branchId, cancellationToken);
        var settings = await EnsureSettingsAsync(clinicId, branchId, cancellationToken);
        return MapSettings(settings);
    }

    public async Task<QueueSettingsDetails> UpdateSettingsAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        string tokenPrefix,
        bool scheduledPriority,
        int earlyMinutes,
        int lateMinutes,
        bool allowWalkIns,
        bool allowPriority,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicOwnerAsync(userId, clinicId, cancellationToken);
        var settings = await EnsureSettingsAsync(clinicId, branchId, cancellationToken);
        settings.Update(tokenPrefix, scheduledPriority, earlyMinutes, lateMinutes, allowWalkIns, allowPriority, clock.UtcNow);
        await queue.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<CheckInTokenDetails> MintQrTokenAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await queue.FindAppointmentAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw DomainException.Conflict("appointment_not_checkable", "Only a confirmed appointment can create a check-in code.");
        }

        var settings = await EnsureSettingsAsync(appointment.ClinicId, appointment.BranchId, cancellationToken);
        var now = clock.UtcNow;
        foreach (var old in await queue.ListActiveTokensAsync(appointmentId, cancellationToken))
        {
            old.Revoke(now);
        }

        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var token = AppointmentCheckInToken.Create(
            appointment.Id,
            appointment.BranchId,
            InfrastructureHash(raw),
            now.Add(TimeSpan.FromMinutes(Math.Max(settings.EarlyCheckInMinutes + settings.LateCheckInMinutes, 30))),
            now);
        queue.AddCheckInToken(token);
        await queue.SaveChangesAsync(cancellationToken);
        return new CheckInTokenDetails(appointment.Id, raw, token.ExpiresAtUtc);
    }

    public async Task<QueueEntryDetails> CheckInByQrAsync(Guid userId, string rawToken, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var hashed = InfrastructureHash(rawToken.Trim());
        var token = await queue.FindCheckInTokenByHashAsync(hashed, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("check_in_token_invalid", "That check-in code was not found.");
        var now = clock.UtcNow;
        if (token.RevokedAtUtc is not null)
        {
            throw DomainException.Conflict("check_in_token_invalid", "That check-in code is no longer valid.");
        }

        if (token.UsedAtUtc is not null)
        {
            throw DomainException.Conflict("already_checked_in", "This appointment is already checked in.");
        }

        if (token.ExpiresAtUtc <= now)
        {
            throw DomainException.Conflict("check_in_token_expired", "That check-in code has expired. Ask the patient to refresh the QR.");
        }

        var appointment = await queue.FindAppointmentAsync(token.AppointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        await access.EnsureDoctorCoverageAsync(userId, appointment.ClinicId, appointment.BranchId, appointment.DoctorId, cancellationToken);
        if (token.BranchId != appointment.BranchId)
        {
            throw DomainException.Conflict("check_in_token_invalid", "That check-in code was not found.");
        }

        token.MarkUsed(now);
        return await CheckInAppointmentCoreAsync(userId, appointment, CheckInMethod.Qr, metadata, cancellationToken);
    }

    public async Task<QueueEntryDetails> CheckInAppointmentAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await queue.FindAppointmentAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        await access.EnsureDoctorCoverageAsync(userId, appointment.ClinicId, appointment.BranchId, appointment.DoctorId, cancellationToken);
        return await CheckInAppointmentCoreAsync(userId, appointment, CheckInMethod.Reception, metadata, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSearchItem>> SearchAppointmentsAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        string? reference,
        string? patientName,
        string? phone,
        Guid? doctorId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        await access.EnsureQueueAccessAsync(userId, clinicId, branchId, cancellationToken);
        var assigned = await access.ListAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
        var allowed = assigned.Count > 0 ? assigned.ToHashSet() : null;
        var rows = await queue.SearchAppointmentsAsync(clinicId, branchId, reference, patientName, phone, doctorId, date, cancellationToken);
        var mapped = new List<AppointmentSearchItem>();
        foreach (var row in rows)
        {
            if (allowed is not null && !allowed.Contains(row.DoctorId))
            {
                continue;
            }

            var doctor = await providers.FindDoctorAsync(row.DoctorId, tracking: false, cancellationToken);
            mapped.Add(new AppointmentSearchItem(
                row.Id,
                row.Reference,
                row.Status.ToString(),
                row.PatientName,
                row.DoctorId,
                doctor?.DisplayName ?? "Doctor",
                row.StartsAtUtc,
                row.IsWalkIn,
                row.TokenLabel));
        }

        return mapped;
    }

    public async Task<WalkInDetails> RegisterWalkInAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        string patientName,
        string? contactNumber,
        string? reason,
        decimal? consultationFee,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await access.EnsureDoctorCoverageAsync(userId, clinicId, branchId, doctorId, cancellationToken);
        var settings = await EnsureSettingsAsync(clinicId, branchId, cancellationToken);
        if (!settings.AllowWalkIns)
        {
            throw DomainException.Conflict("walk_ins_disabled", "Walk-ins are not enabled for this branch.");
        }

        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.Branches.All(item => item.Id != branchId || !item.IsActive))
        {
            throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        }

        var doctor = await providers.FindDoctorAsync(doctorId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
        var affiliations = await providers.ListAffiliationsAsync(doctorId, cancellationToken);
        if (!affiliations.Any(item => item.ClinicId == clinicId && (item.BranchId is null || item.BranchId == branchId)))
        {
            throw DomainException.NotFound("doctor_not_found", "That doctor was not found at this clinic.");
        }

        var fee = consultationFee ?? await FeeForAsync(doctorId, clinicId, branchId, cancellationToken);
        var now = clock.UtcNow;
        var operatingDate = LocalDate(now, ConsultationSchedule.DefaultTimeZone);
        var walkIn = WalkInRecord.Create(clinicId, branchId, doctorId, patientName, contactNumber, reason, fee, userId, now);
        queue.AddWalkIn(walkIn);
        var session = await EnsureSessionAsync(clinicId, branchId, doctorId, operatingDate, now, cancellationToken);
        var (label, number) = await IssueTokenAsync(clinicId, branchId, doctorId, operatingDate, settings.TokenPrefix, cancellationToken);
        var position = await NextPositionAsync(doctorId, branchId, operatingDate, cancellationToken);
        var entry = QueueEntry.CheckInWalkIn(session, walkIn.Id, walkIn.PatientDisplayName, label, number, position, now);
        queue.AddEntry(entry);
        queue.AddHistory(QueueStatusHistory.Record(entry.Id, QueueEntryStatus.Waiting, QueueEntryStatus.Waiting, userId, "walk-in", now));
        await queue.AddAuditAsync(AuditLog.Record(userId, AuditActions.QueueEntryCreated, now, metadata.IpAddress, metadata.UserAgent), cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
        await ReorderAsync(doctorId, branchId, operatingDate, settings, cancellationToken);
        await PublishQueueAsync(entry, "PatientCheckedIn", cancellationToken);
        logger.LogInformation("Walk-in queue entry {EntryId} created", entry.Id);
        return new WalkInDetails(walkIn.Id, entry.Id, entry.TokenLabel, walkIn.PatientDisplayName, doctorId, doctor.DisplayName ?? "Doctor", walkIn.ConsultationFee, walkIn.Currency, walkIn.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<QueueEntryDetails>> ListDoctorQueueAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        await access.EnsureDoctorQueueAsync(userId, doctorId, clinicId, branchId, cancellationToken);
        var day = date ?? LocalDate(clock.UtcNow, ConsultationSchedule.DefaultTimeZone);
        var rows = await queue.ListDoctorQueueAsync(doctorId, branchId, day, cancellationToken);
        return await MapEntriesAsync(rows, includeEstimate: true, cancellationToken);
    }

    public async Task<QueueSummary> DoctorSummaryAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var rows = await ListDoctorQueueAsync(userId, clinicId, branchId, doctorId, null, cancellationToken);
        static bool IsPaid(string? status) =>
            string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);
        return new QueueSummary(
            rows.Count,
            rows.Count(item => item.Status == nameof(QueueEntryStatus.Waiting)),
            rows.Count(item => item.Status == nameof(QueueEntryStatus.Called)),
            rows.Count(item => item.Status == nameof(QueueEntryStatus.InConsultation)),
            rows.Count(item => item.Status == nameof(QueueEntryStatus.Completed)),
            rows.Count(item => item.IsWalkIn),
            rows.FirstOrDefault(item => item.Status == nameof(QueueEntryStatus.InConsultation) || item.Status == nameof(QueueEntryStatus.Called)),
            rows.FirstOrDefault(item => item.Status == nameof(QueueEntryStatus.Waiting)),
            rows.Count(item => item.Status == nameof(QueueEntryStatus.Skipped)),
            rows.Count(item => IsPaid(item.PaymentStatus)),
            rows.Count(item => !IsPaid(item.PaymentStatus)));
    }

    public async Task<IReadOnlyList<QueueEntryDetails>> ListBranchQueueAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        await access.EnsureQueueAccessAsync(userId, clinicId, branchId, cancellationToken);
        var day = date ?? LocalDate(clock.UtcNow, ConsultationSchedule.DefaultTimeZone);
        return await MapEntriesAsync(await queue.ListBranchQueueAsync(branchId, day, cancellationToken), includeEstimate: false, cancellationToken);
    }

    public async Task<IReadOnlyList<QueueEntryDetails>> ListMyQueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await MapEntriesAsync(await queue.ListPatientQueueAsync(userId, cancellationToken), includeEstimate: true, cancellationToken);

    public async Task<QueueEntryDetails> GetMyEntryAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await queue.FindEntryAsync(entryId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("queue_entry_not_found", "That queue entry was not found.");
        if (entry.AccountUserId != userId)
        {
            throw DomainException.NotFound("queue_entry_not_found", "That queue entry was not found.");
        }

        return (await MapEntriesAsync([entry], includeEstimate: true, cancellationToken))[0];
    }

    public Task<QueueEntryDetails> CallAsync(Guid userId, Guid entryId, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Call(actor, clock.UtcNow), "PatientCalled", cancellationToken);

    public Task<QueueEntryDetails> RecallAsync(Guid userId, Guid entryId, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Recall(actor, clock.UtcNow), "PatientRecalled", cancellationToken);

    public Task<QueueEntryDetails> StartAsync(Guid userId, Guid entryId, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.StartConsultation(actor, clock.UtcNow), "ConsultationStarted", cancellationToken);

    public async Task<QueueEntryDetails> CompleteAsync(Guid userId, Guid entryId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var details = await MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Complete(actor, clock.UtcNow), "ConsultationCompleted", cancellationToken);
        var entry = await queue.FindEntryAsync(entryId, tracking: true, cancellationToken);
        if (entry?.AppointmentId is Guid appointmentId)
        {
            var appointment = await queue.FindAppointmentAsync(appointmentId, tracking: true, cancellationToken);
            if (appointment is not null && appointment.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Confirmed)
            {
                appointment.MarkCompleted(clock.UtcNow);
                await queue.AddAuditAsync(AuditLog.Record(userId, AuditActions.AppointmentCompleted, clock.UtcNow, metadata.IpAddress, metadata.UserAgent), cancellationToken);
                await queue.SaveChangesAsync(cancellationToken);
                await finance.CaptureCommissionForCompletedAppointmentAsync(appointmentId, cancellationToken);
                if (!appointment.IsWalkIn)
                {
                    await reviews.NotifyReviewRequestAsync(appointment.AccountUserId, appointment.Id, cancellationToken);
                    await growth.OnAppointmentCompletedAsync(appointmentId, cancellationToken);
                }
            }
        }

        return details;
    }

    public Task<QueueEntryDetails> SkipAsync(Guid userId, Guid entryId, string? reason, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Skip(actor, reason, clock.UtcNow), "PatientSkipped", cancellationToken);

    public Task<QueueEntryDetails> HoldAsync(Guid userId, Guid entryId, string? reason, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Hold(actor, reason, clock.UtcNow), "QueuePaused", cancellationToken);

    public Task<QueueEntryDetails> ResumeAsync(Guid userId, Guid entryId, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Resume(actor, clock.UtcNow), "QueueResumed", cancellationToken);

    public Task<QueueEntryDetails> CancelEntryAsync(Guid userId, Guid entryId, string? reason, RequestMetadata metadata, CancellationToken cancellationToken) =>
        MutateAsync(userId, entryId, metadata, (entry, actor) => entry.Cancel(actor, reason, clock.UtcNow), "QueueEntryCancelled", cancellationToken);

    public async Task<QueueEntryDetails?> CallNextAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await access.EnsureDoctorQueueAsync(userId, doctorId, clinicId, branchId, cancellationToken);
        var day = LocalDate(clock.UtcNow, ConsultationSchedule.DefaultTimeZone);
        var rows = await queue.ListDoctorQueueAsync(doctorId, branchId, day, cancellationToken);
        if (rows.Any(item => item.Status == QueueEntryStatus.InConsultation))
        {
            throw DomainException.Conflict("consultation_active", "Finish the current consultation before calling the next patient.");
        }

        var next = rows.Where(item => item.Status == QueueEntryStatus.Waiting).OrderBy(item => item.QueuePosition).FirstOrDefault();
        return next is null ? null : await CallAsync(userId, next.Id, metadata, cancellationToken);
    }

    public async Task<QueueDisplayBoard> DisplayAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        await access.EnsureDoctorQueueAsync(userId, doctorId, clinicId, branchId, cancellationToken);
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken);
        var branch = clinic?.Branches.FirstOrDefault(item => item.Id == branchId);
        var doctor = await providers.FindDoctorAsync(doctorId, tracking: false, cancellationToken);
        var day = LocalDate(clock.UtcNow, ConsultationSchedule.DefaultTimeZone);
        var rows = (await queue.ListDoctorQueueAsync(doctorId, branchId, day, cancellationToken))
            .Where(item => item.IsActive || item.Status == QueueEntryStatus.InConsultation)
            .OrderBy(item => item.QueuePosition)
            .ToArray();
        var current = rows.FirstOrDefault(item => item.Status is QueueEntryStatus.InConsultation or QueueEntryStatus.Called);
        var waiting = rows.Where(item => item.Status == QueueEntryStatus.Waiting).ToArray();
        return new QueueDisplayBoard(
            clinic?.Name ?? "Clinic",
            branch?.Name ?? "Branch",
            doctor?.DisplayName ?? "Doctor",
            current?.TokenLabel,
            waiting.ElementAtOrDefault(0)?.TokenLabel,
            waiting.ElementAtOrDefault(1)?.TokenLabel,
            current?.Status.ToString() ?? "Idle",
            clock.UtcNow);
    }

    public async Task<IReadOnlyList<AdminQueueSnapshot>> AdminSnapshotsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber > 31)
        {
            throw DomainException.Validation("date_invalid", "Choose a range of 31 days or fewer.");
        }

        var snapshots = new List<AdminQueueSnapshot>();
        var entries = await ListAdminEntriesAsync(from, to, cancellationToken);
        foreach (var group in entries.GroupBy(item => new { item.ClinicId, item.BranchId, item.DoctorId, item.OperatingDate }))
        {
            var clinic = await providers.FindClinicAsync(group.Key.ClinicId, tracking: false, cancellationToken);
            var branch = clinic?.Branches.FirstOrDefault(item => item.Id == group.Key.BranchId);
            var doctor = await providers.FindDoctorAsync(group.Key.DoctorId, tracking: false, cancellationToken);
            var rows = group.ToArray();
            var waits = rows.Where(item => item.CalledAtUtc is not null).Select(item => (item.CalledAtUtc!.Value - item.CheckInAtUtc).TotalMinutes).ToArray();
            var consults = rows.Where(item => item.ConsultationStartedAtUtc is not null && item.ConsultationCompletedAtUtc is not null)
                .Select(item => (item.ConsultationCompletedAtUtc!.Value - item.ConsultationStartedAtUtc!.Value).TotalMinutes).ToArray();
            snapshots.Add(new AdminQueueSnapshot(
                group.Key.ClinicId,
                clinic?.Name ?? "Clinic",
                group.Key.BranchId,
                branch?.Name ?? "Branch",
                group.Key.DoctorId,
                doctor?.DisplayName ?? "Doctor",
                group.Key.OperatingDate,
                rows.Count(item => item.Status == QueueEntryStatus.Waiting),
                rows.Count(item => item.Status == QueueEntryStatus.InConsultation),
                rows.Count(item => item.Status == QueueEntryStatus.Completed),
                rows.Count(item => item.IsWalkIn),
                rows.Count(item => item.Status == QueueEntryStatus.Skipped),
                waits.Length == 0 ? null : waits.Average(),
                consults.Length == 0 ? null : consults.Average(),
                "Open"));
        }

        return snapshots;
    }

    public async Task<IReadOnlyList<NotificationDetails>> ListNotificationsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await queue.ListNotificationsAsync(userId, cancellationToken))
            .Select(item => new NotificationDetails(
                item.Id,
                item.Kind,
                item.Title,
                item.Body,
                item.CreatedAtUtc,
                item.ReadAtUtc is not null,
                item.SubjectId,
                NotificationDeepLinks.Resolve(item.Kind, item.SubjectId)))
            .ToArray();

    private Task<IReadOnlyList<QueueEntry>> ListAdminEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        queue.ListEntriesBetweenAsync(from, to, cancellationToken);

    private async Task<QueueEntryDetails> CheckInAppointmentCoreAsync(
        Guid userId,
        Appointment appointment,
        CheckInMethod method,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw DomainException.Conflict("appointment_not_checkable", "Only a confirmed appointment can be checked in.");
        }

        if (await queue.FindActiveByAppointmentAsync(appointment.Id, cancellationToken) is not null)
        {
            throw DomainException.Conflict("already_checked_in", "This appointment is already in the queue.");
        }

        var settings = await EnsureSettingsAsync(appointment.ClinicId, appointment.BranchId, cancellationToken);
        var now = clock.UtcNow;
        EnsureWithinCheckInWindow(appointment, settings, now);
        var operatingDate = LocalDate(appointment.StartsAtUtc, appointment.TimeZoneId);
        var isLate = now > appointment.StartsAtUtc;
        appointment.MarkCheckedIn(now);
        var session = await EnsureSessionAsync(appointment.ClinicId, appointment.BranchId, appointment.DoctorId, operatingDate, now, cancellationToken);
        string label;
        int number;
        if (!string.IsNullOrWhiteSpace(appointment.TokenLabel) && appointment.TokenNumber is int existingNumber)
        {
            label = appointment.TokenLabel;
            number = existingNumber;
        }
        else
        {
            (label, number) = await IssueTokenAsync(
                appointment.ClinicId,
                appointment.BranchId,
                appointment.DoctorId,
                operatingDate,
                settings.TokenPrefix,
                cancellationToken);
            appointment.AssignVisitToken(label, number, now);
        }

        var position = await NextPositionAsync(appointment.DoctorId, appointment.BranchId, operatingDate, cancellationToken);
        var entry = QueueEntry.CheckInAppointment(
            session,
            appointment.Id,
            appointment.AccountUserId,
            appointment.PatientName,
            label,
            number,
            position,
            method,
            isLate,
            appointment.StartsAtUtc,
            now);
        queue.AddEntry(entry);
        queue.AddHistory(QueueStatusHistory.Record(entry.Id, QueueEntryStatus.Waiting, QueueEntryStatus.Waiting, userId, method.ToString(), now));
        await queue.AddAuditAsync(AuditLog.Record(userId, AuditActions.AppointmentCheckedIn, now, metadata.IpAddress, metadata.UserAgent), cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
        await ReorderAsync(appointment.DoctorId, appointment.BranchId, operatingDate, settings, cancellationToken);
        await PublishQueueAsync(entry, "PatientCheckedIn", cancellationToken);
        try
        {
            var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
            var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
            var doctorName = !string.IsNullOrWhiteSpace(doctor?.DisplayName)
                ? doctor!.DisplayName!
                : "Doctor";
            await automation.PublishAsync(
                AutomationTriggerTypes.PatientCheckedIn,
                new
                {
                    queueEntryId = entry.Id,
                    appointmentId = appointment.Id,
                    accountUserId = appointment.AccountUserId,
                    userId = appointment.AccountUserId,
                    patientName = appointment.PatientName,
                    doctorName,
                    clinicName = clinic?.Name ?? "Clinic",
                    reference = appointment.Reference,
                    tokenLabel = entry.TokenLabel,
                    position = entry.QueuePosition,
                    status = appointment.Status.ToString(),
                    startsAtUtc = appointment.StartsAtUtc,
                    endsAtUtc = appointment.EndsAtUtc,
                    timeZoneId = appointment.TimeZoneId,
                    bookingMode = appointment.BookingMode.ToString(),
                    clinicId = appointment.ClinicId,
                    branchId = appointment.BranchId,
                    doctorId = appointment.DoctorId
                },
                $"queue.checkin:{entry.Id:N}",
                cancellationToken,
                subjectId: appointment.Id,
                userId: appointment.AccountUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish check-in automation for entry {EntryId}", entry.Id);
        }

        logger.LogInformation("Appointment {Reference} checked in as {Token}", appointment.Reference, entry.TokenLabel);
        return (await MapEntriesAsync([entry], includeEstimate: true, cancellationToken))[0];
    }

    private async Task<QueueEntryDetails> MutateAsync(
        Guid userId,
        Guid entryId,
        RequestMetadata metadata,
        Func<QueueEntry, Guid, QueueStatusHistory> action,
        string eventName,
        CancellationToken cancellationToken)
    {
        var entry = await queue.FindEntryAsync(entryId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("queue_entry_not_found", "That queue entry was not found.");
        await access.EnsureDoctorQueueAsync(userId, entry.DoctorId, entry.ClinicId, entry.BranchId, cancellationToken);
        var history = action(entry, userId);
        queue.AddHistory(history);
        await queue.AddAuditAsync(AuditLog.Record(userId, AuditActions.QueueEntryChanged, clock.UtcNow, metadata.IpAddress, metadata.UserAgent), cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
        if (entry.AccountUserId is Guid patientId)
        {
            await NotifyPatientAsync(patientId, eventName, entry.Id, StatusTitle(entry.Status), StatusMessage(entry.Status, 0), cancellationToken);
        }

        await PublishQueueAsync(entry, eventName, cancellationToken);
        try
        {
            await automation.PublishAsync(
                AutomationTriggerTypes.QueuePositionChanged,
                new
                {
                    queueEntryId = entry.Id,
                    userId = entry.AccountUserId,
                    status = entry.Status.ToString(),
                    position = entry.QueuePosition,
                    eventName
                },
                $"queue.pos:{entry.Id:N}:{entry.Status}:{entry.QueuePosition}:{entry.UpdatedAtUtc.UtcTicks}",
                cancellationToken,
                subjectId: entry.Id,
                userId: entry.AccountUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish queue automation for entry {EntryId}", entry.Id);
        }

        return (await MapEntriesAsync([entry], includeEstimate: true, cancellationToken))[0];
    }

    private async Task<ClinicQueueSettings> EnsureSettingsAsync(Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        var settings = await queue.FindSettingsAsync(clinicId, branchId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = ClinicQueueSettings.CreateDefault(clinicId, branchId, clock.UtcNow);
        queue.AddSettings(settings);
        await queue.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<ClinicQueueSession> EnsureSessionAsync(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly date,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await queue.FindSessionAsync(clinicId, branchId, doctorId, date, tracking: true, cancellationToken);
        if (session is not null)
        {
            if (session.Status == QueueSessionStatus.Closed)
            {
                throw DomainException.Conflict("queue_closed", "This doctor's queue is closed for the day.");
            }

            return session;
        }

        session = ClinicQueueSession.Open(clinicId, branchId, doctorId, date, now);
        queue.AddSession(session);
        await queue.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task<(string Label, int Number)> IssueTokenAsync(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly date,
        string prefix,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var counter = await queue.FindCounterAsync(clinicId, branchId, doctorId, date, cancellationToken);
                if (counter is null)
                {
                    counter = QueueTokenCounter.Start(clinicId, branchId, doctorId, date);
                    queue.AddCounter(counter);
                }

                var number = counter.Next();
                await queue.SaveTokenIssueAsync(cancellationToken);
                return ($"{prefix}{number}", number);
            }
            catch (DomainException exception) when (exception.Code == "queue_conflict" && attempt == 0)
            {
                // retry once
            }
        }

        throw DomainException.Conflict("token_issue_failed", "Could not issue a queue token. Try again.");
    }

    private async Task<int> NextPositionAsync(Guid doctorId, Guid branchId, DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await queue.ListDoctorQueueAsync(doctorId, branchId, date, cancellationToken);
        return rows.Count == 0 ? 1 : rows.Max(item => item.QueuePosition) + 1;
    }

    private async Task ReorderAsync(
        Guid doctorId,
        Guid branchId,
        DateOnly date,
        ClinicQueueSettings settings,
        CancellationToken cancellationToken)
    {
        var rows = (await queue.ListDoctorQueueAsync(doctorId, branchId, date, cancellationToken))
            .Where(item => item.Status is QueueEntryStatus.Waiting or QueueEntryStatus.OnHold)
            .ToList();
        IEnumerable<QueueEntry> ordered = settings.ScheduledPatientPriority
            ? rows.OrderBy(item => item.IsWalkIn)
                .ThenBy(item => item.IsLate)
                .ThenBy(item => item.AppointmentStartsAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(item => item.CheckInAtUtc)
            : rows.OrderBy(item => item.CheckInAtUtc);
        var position = 1;
        foreach (var row in ordered)
        {
            var tracked = await queue.FindEntryAsync(row.Id, tracking: true, cancellationToken);
            tracked?.SetPosition(position++, clock.UtcNow);
        }

        await queue.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureWithinCheckInWindow(Appointment appointment, ClinicQueueSettings settings, DateTimeOffset now)
    {
        var earliest = appointment.StartsAtUtc.AddMinutes(-settings.EarlyCheckInMinutes);
        var latest = appointment.StartsAtUtc.AddMinutes(settings.LateCheckInMinutes);
        if (now < earliest)
        {
            throw DomainException.Validation("check_in_too_early", "Check-in is not open yet for this appointment.");
        }

        if (now > latest)
        {
            throw DomainException.Validation("check_in_too_late", "The check-in window for this appointment has closed.");
        }
    }

    private async Task<decimal> FeeForAsync(Guid doctorId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        var published = await schedules.ListPublishedForDoctorAsync(doctorId, cancellationToken);
        var match = published.FirstOrDefault(item => item.ClinicId == clinicId && item.BranchId == branchId);
        return match?.ConsultationFee ?? 0;
    }

    private async Task NotifyPatientAsync(Guid userId, string kind, Guid subjectId, string title, string body, CancellationToken cancellationToken)
    {
        var since = clock.UtcNow.Subtract(QueueDefaults.NotificationCooldown);
        if (await queue.HasRecentNotificationAsync(userId, kind, subjectId, since, cancellationToken))
        {
            return;
        }

        var notification = UserNotification.Create(userId, kind, subjectId, title, body, clock.UtcNow);
        await queue.NotifyAsync(notification, cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
        await realtime.PublishAsync(
            $"user:{userId}",
            "notification",
            new
            {
                notification.Id,
                notification.Kind,
                notification.Title,
                notification.Body,
                DeepLink = NotificationDeepLinks.Resolve(kind, subjectId)
            },
            cancellationToken);
        try
        {
            await mobileDevices.DispatchUserPushAsync(userId, kind, subjectId, title, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push dispatch failed for user {UserId} kind {Kind}", userId, kind);
        }
    }

    private async Task PublishQueueAsync(QueueEntry entry, string eventName, CancellationToken cancellationToken)
    {
        var payload = new
        {
            entry.Id,
            entry.TokenLabel,
            Status = entry.Status.ToString(),
            entry.DoctorId,
            entry.BranchId,
            entry.OperatingDate
        };
        await realtime.PublishAsync($"queue:doctor:{entry.DoctorId}:{entry.BranchId}:{entry.OperatingDate:yyyy-MM-dd}", eventName, payload, cancellationToken);
        await realtime.PublishAsync($"queue:branch:{entry.BranchId}", eventName, payload, cancellationToken);
        await realtime.PublishAsync($"queue:entry:{entry.Id}", eventName, payload, cancellationToken);
    }

    private async Task<IReadOnlyList<QueueEntryDetails>> MapEntriesAsync(
        IReadOnlyList<QueueEntry> rows,
        bool includeEstimate,
        CancellationToken cancellationToken)
    {
        var mapped = new List<QueueEntryDetails>();
        foreach (var row in rows)
        {
            var doctor = await providers.FindDoctorAsync(row.DoctorId, tracking: false, cancellationToken);
            var clinic = await providers.FindClinicAsync(row.ClinicId, tracking: false, cancellationToken);
            var branch = clinic?.Branches.FirstOrDefault(item => item.Id == row.BranchId);
            Appointment? appointment = null;
            if (row.AppointmentId is Guid appointmentId)
            {
                appointment = await queue.FindAppointmentAsync(appointmentId, tracking: false, cancellationToken);
            }

            int? ahead = null;
            int? estimate = null;
            if (includeEstimate && row.IsActive)
            {
                ahead = await queue.CountActiveAheadAsync(row, cancellationToken);
                var average = await queue.AverageConsultationMinutesAsync(row.DoctorId, row.BranchId, cancellationToken) ?? 12;
                estimate = (int)Math.Ceiling((ahead.Value + (row.Status == QueueEntryStatus.InConsultation ? 0.5 : 1)) * average);
            }

            mapped.Add(new QueueEntryDetails(
                row.Id,
                row.TokenLabel,
                row.TokenNumber,
                row.QueuePosition,
                row.Status.ToString(),
                row.DisplayName,
                row.DoctorId,
                doctor?.DisplayName ?? "Doctor",
                row.ClinicId,
                clinic?.Name ?? "Clinic",
                row.BranchId,
                branch?.Name ?? "Branch",
                row.AppointmentId,
                appointment?.Reference,
                row.IsWalkIn,
                row.IsLate,
                row.CheckInAtUtc,
                row.AppointmentStartsAtUtc,
                row.CalledAtUtc,
                row.ConsultationStartedAtUtc,
                ahead,
                estimate,
                StatusMessage(row.Status, ahead ?? 0),
                row.UpdatedAtUtc,
                appointment?.PaymentStatus,
                appointment?.PaymentMethod));
        }

        return mapped;
    }

    private static QueueSettingsDetails MapSettings(ClinicQueueSettings settings) =>
        new(
            settings.ClinicId,
            settings.BranchId,
            settings.TokenPrefix,
            settings.ScheduledPatientPriority,
            settings.EarlyCheckInMinutes,
            settings.LateCheckInMinutes,
            settings.AllowWalkIns,
            settings.AllowPriorityPatients);

    private static DateOnly LocalDate(DateTimeOffset instant, string timeZoneId)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }

    private static string StatusTitle(QueueEntryStatus status) => status switch
    {
        QueueEntryStatus.Waiting => "Please wait",
        QueueEntryStatus.Called => "Please proceed",
        QueueEntryStatus.InConsultation => "Consultation started",
        QueueEntryStatus.Completed => "Visit completed",
        QueueEntryStatus.Skipped => "Missed call",
        QueueEntryStatus.OnHold => "On hold",
        _ => "Queue update"
    };

    private static string StatusMessage(QueueEntryStatus status, int ahead) => status switch
    {
        QueueEntryStatus.Waiting when ahead <= 1 => "Your turn is coming soon.",
        QueueEntryStatus.Waiting => "Please wait for your turn.",
        QueueEntryStatus.Called => "Please proceed to the consultation room.",
        QueueEntryStatus.InConsultation => "Your consultation is in progress.",
        QueueEntryStatus.Completed => "Your visit is completed.",
        QueueEntryStatus.Skipped => "Please speak with reception.",
        QueueEntryStatus.OnHold => "Your visit is temporarily on hold.",
        _ => "You are checked in."
    };

    private static string InfrastructureHash(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
