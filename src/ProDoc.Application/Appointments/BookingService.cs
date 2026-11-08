using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Clinical;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Appointments;

public sealed class BookingService(
    IAppointmentStore appointments,
    IScheduleStore schedules,
    ScheduleService scheduleService,
    IProviderStore providers,
    IClinicAccess clinicAccess,
    IQueueStore queueStore,
    IAuthStore auth,
    IAutomationEventBus automation,
    FollowUpService followUps,
    IPlaceLinkBuilder placeLinks,
    IClock clock,
    ILogger<BookingService> logger)
{
    public async Task<AppResult<AppointmentDetails>> HoldAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await BookableScheduleAsync(scheduleId, date, start, cancellationToken, AvailabilityHorizon.PatientTwoWeeks);
        if (scheduleResult.IsFailure)
        {
            return AppResult<AppointmentDetails>.Failure(scheduleResult.Error!);
        }

        var schedule = scheduleResult.Value!;
        var slotResult = await RequireSlotAsync(scheduleId, date, start, cancellationToken, horizon: AvailabilityHorizon.PatientTwoWeeks);
        if (slotResult.IsFailure)
        {
            return AppResult<AppointmentDetails>.Failure(slotResult.Error!);
        }

        var slot = slotResult.Value!;
        var starts = ToUtc(date, slot.Start, schedule.TimeZoneId);
        var ends = ToUtc(date, slot.End, schedule.TimeZoneId);
        var now = clock.UtcNow;
        if (schedule.RequiresApproval && starts <= now.AddMinutes(30))
        {
            return AppResult<AppointmentDetails>.Validation(
                "approval_window_closed",
                "This visit is too soon for an approval request.");
        }

        if (await FindDuplicateActiveVisitErrorAsync(
                userId,
                schedule.DoctorId,
                date,
                exceptAppointmentId: null,
                cancellationToken) is { } duplicate)
        {
            return AppResult<AppointmentDetails>.Failure(duplicate);
        }

        var held = Appointment.Hold(
            userId,
            schedule.DoctorId,
            schedule.ClinicId!.Value,
            schedule.BranchId!.Value,
            schedule.Id,
            starts,
            ends,
            schedule.TimeZoneId,
            schedule.ConsultationFee,
            schedule.RequiresApproval,
            now,
            schedule.BookingMode);
        await CaptureAppointmentLocationAsync(held, cancellationToken);
        await appointments.AddHoldAsync(held, cancellationToken);
        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentHeld, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {AppointmentId} held", held.Id);
        return AppResult<AppointmentDetails>.Success(await MapAsync(held, includeClinical: true, cancellationToken));
    }

    public async Task<AppResult<AppointmentDetails>> ConfirmAsync(
        Guid userId,
        Guid appointmentId,
        Guid? familyMemberId,
        string? reason,
        string? notes,
        RequestMetadata metadata,
        CancellationToken cancellationToken,
        Guid? followUpId = null)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken);
        if (appointment is null || appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            return AppResult<AppointmentDetails>.NotFound(
                "appointment_not_found",
                "That appointment was not found.");
        }

        var now = clock.UtcNow;
        string patientName;
        try
        {
            patientName = await PatientNameAsync(userId, familyMemberId, cancellationToken);
        }
        catch (DomainException exception)
        {
            return AppResult<AppointmentDetails>.FromException(exception);
        }

        if (appointment.BookingMode == ScheduleBookingMode.HourlyQueue)
        {
            // Re-validate capacity at confirm so concurrent holds cannot overbook a full hour.
            var local = TimeZoneInfo.ConvertTime(appointment.StartsAtUtc, Zone(appointment.TimeZoneId));
            var slotCheck = await RequireSlotAsync(
                appointment.ScheduleId,
                DateOnly.FromDateTime(local.DateTime),
                TimeOnly.FromDateTime(local.DateTime),
                cancellationToken,
                appointment.Id,
                AvailabilityHorizon.PatientTwoWeeks);
            if (slotCheck.IsFailure)
            {
                return AppResult<AppointmentDetails>.Failure(slotCheck.Error!);
            }
        }

        if (appointment.HoldExpiresAtUtc is not null && appointment.HoldExpiresAtUtc <= now)
        {
            await appointments.SaveChangesAsync(null, cancellationToken);
            return AppResult<AppointmentDetails>.Validation(
                "hold_expired",
                "Your hold expired. Please select the time again.");
        }

        try
        {
            appointment.Confirm(patientName, familyMemberId, reason, notes, now);
        }
        catch (DomainException exception)
        {
            if (exception.Code == "hold_expired")
            {
                await appointments.SaveChangesAsync(null, cancellationToken);
            }

            return AppResult<AppointmentDetails>.FromException(exception);
        }

        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            await EnsureVisitTokenAsync(appointment, cancellationToken);
        }

        var action = appointment.Status == AppointmentStatus.Requested
            ? AuditActions.AppointmentRequested
            : AuditActions.AppointmentConfirmed;
        await appointments.SaveChangesAsync(Audit(userId, action, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {Reference} is {Status}", appointment.Reference, appointment.Status);
        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            await PublishAppointmentAsync(AutomationTriggerTypes.AppointmentConfirmed, appointment, cancellationToken);
        }
        else if (appointment.Status == AppointmentStatus.Requested)
        {
            await PublishAppointmentAsync(AutomationTriggerTypes.AppointmentCreated, appointment, cancellationToken);
        }

        await followUps.TryLinkAfterBookingAsync(
            userId,
            followUpId,
            appointment.Id,
            appointment.DoctorId,
            metadata,
            cancellationToken);

        return AppResult<AppointmentDetails>.Success(await MapAsync(appointment, includeClinical: true, cancellationToken));
    }

    public async Task<AppResult> ReleaseAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken);
        if (appointment is null || appointment.AccountUserId != userId)
        {
            return AppResult.NotFound("appointment_not_found", "That appointment was not found.");
        }

        try
        {
            appointment.Release(clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return AppResult.FromException(exception);
        }

        await appointments.SaveChangesAsync(null, cancellationToken);
        return AppResult.Success();
    }

    public async Task<IReadOnlyList<AppointmentDetails>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await appointments.ListForAccountAsync(userId, cancellationToken);
        rows = await BackfillMissingVisitTokensAsync(rows.ToArray(), cancellationToken);
        return await MapAllAsync(rows, includeClinical: true, cancellationToken);
    }

    public async Task<AppResult<AppointmentDetails>> GetAsync(
        Guid userId,
        Guid appointmentId,
        bool practice,
        CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: false, cancellationToken);
        if (appointment is null)
        {
            return AppResult<AppointmentDetails>.NotFound(
                "appointment_not_found",
                "That appointment was not found.");
        }

        var owns = appointment.AccountUserId == userId && !appointment.IsWalkIn;
        var manages = practice && await ManagesAsync(userId, appointment, cancellationToken);
        if (!owns && !manages)
        {
            return AppResult<AppointmentDetails>.NotFound(
                "appointment_not_found",
                "That appointment was not found.");
        }

        if (owns && appointment.Status == AppointmentStatus.Confirmed && appointment.TokenNumber is null)
        {
            var tracked = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
                ?? appointment;
            await EnsureVisitTokenIfNeededAsync(tracked, cancellationToken);
            appointment = tracked;
        }

        return AppResult<AppointmentDetails>.Success(await MapAsync(appointment, includeClinical: true, cancellationToken));
    }

    public async Task<AppResult<AppointmentDetails>> CancelAsync(
        Guid userId,
        Guid appointmentId,
        string? reason,
        bool practice,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        Appointment appointment;
        try
        {
            appointment = await MutableAsync(userId, appointmentId, practice, cancellationToken);
        }
        catch (DomainException exception)
        {
            return AppResult<AppointmentDetails>.FromException(exception);
        }

        var now = clock.UtcNow;
        try
        {
            appointment.Cancel(staff: practice && await ManagesAsync(userId, appointment, cancellationToken), reason, now);
        }
        catch (DomainException exception)
        {
            return AppResult<AppointmentDetails>.FromException(exception);
        }

        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentCancelled, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {Reference} cancelled", appointment.Reference);
        await PublishAppointmentAsync(AutomationTriggerTypes.AppointmentCancelled, appointment, cancellationToken);
        return AppResult<AppointmentDetails>.Success(await MapAsync(appointment, includeClinical: true, cancellationToken));
    }

    public async Task<AppResult<AppointmentDetails>> RescheduleAsync(
        Guid userId,
        Guid appointmentId,
        DateOnly date,
        TimeOnly start,
        bool practice,
        RequestMetadata metadata,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        Appointment appointment;
        try
        {
            appointment = await MutableAsync(userId, appointmentId, practice, cancellationToken);
        }
        catch (DomainException exception)
        {
            return AppResult<AppointmentDetails>.FromException(exception);
        }

        var staff = practice && await ManagesAsync(userId, appointment, cancellationToken);
        var now = clock.UtcNow;
        var previousOperatingDate = LocalOperatingDate(appointment.StartsAtUtc, appointment.TimeZoneId);
        var previousStarts = appointment.StartsAtUtc;
        var previousEnds = appointment.EndsAtUtc;
        var sameDayMissed = !staff
            && appointment.Status == AppointmentStatus.Confirmed
            && appointment.StartsAtUtc <= now
            && previousOperatingDate == date;

        if (sameDayMissed)
        {
            try
            {
                await EnsureSameDayRescheduleWindowAsync(appointment, date, now, cancellationToken);
            }
            catch (DomainException exception)
            {
                return AppResult<AppointmentDetails>.FromException(exception);
            }
        }

        var horizon = staff || sameDayMissed ? AvailabilityHorizon.StaffExtended : AvailabilityHorizon.PatientTwoWeeks;
        // Same-day missed moves stay on today's date; allow slots after now via StaffExtended date window
        // but RequireSlot still filters past times in FilterPast.
        var slotResult = await RequireSlotAsync(
            appointment.ScheduleId,
            date,
            start,
            cancellationToken,
            appointment.Id,
            sameDayMissed ? AvailabilityHorizon.StaffExtended : horizon);
        if (slotResult.IsFailure)
        {
            return AppResult<AppointmentDetails>.Failure(slotResult.Error!);
        }

        var slot = slotResult.Value!;
        var schedule = await schedules.FindAsync(appointment.ScheduleId, tracking: false, cancellationToken);
        if (schedule is null)
        {
            return AppResult<AppointmentDetails>.NotFound("schedule_not_found", "That schedule is not available.");
        }

        var starts = ToUtc(date, slot.Start, schedule.TimeZoneId);
        var ends = ToUtc(date, slot.End, schedule.TimeZoneId);

        if (appointment.AccountUserId != Guid.Empty
            && await FindDuplicateActiveVisitErrorAsync(
                appointment.AccountUserId,
                appointment.DoctorId,
                date,
                exceptAppointmentId: appointment.Id,
                cancellationToken) is { } duplicate)
        {
            return AppResult<AppointmentDetails>.Failure(duplicate);
        }

        try
        {
            appointment.Reschedule(starts, ends, staff, now, reason, sameDayMissed);
        }
        catch (DomainException exception)
        {
            return AppResult<AppointmentDetails>.FromException(exception);
        }

        var newOperatingDate = LocalOperatingDate(starts, schedule.TimeZoneId);
        var rangeChanged = previousStarts != starts || previousEnds != ends;
        if (previousOperatingDate != newOperatingDate
            || (appointment.BookingMode == ScheduleBookingMode.HourlyQueue && rangeChanged))
        {
            appointment.ClearVisitToken(now);
            if (appointment.Status == AppointmentStatus.Confirmed)
            {
                await EnsureVisitTokenAsync(appointment, cancellationToken);
            }
        }

        await appointments.SaveMoveAsync(appointment, Audit(userId, AuditActions.AppointmentRescheduled, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {Reference} rescheduled", appointment.Reference);
        await PublishAppointmentAsync(
            AutomationTriggerTypes.AppointmentRescheduled,
            appointment,
            cancellationToken,
            previousStartsAtUtc: previousStarts,
            previousEndsAtUtc: previousEnds);
        return AppResult<AppointmentDetails>.Success(await MapAsync(appointment, includeClinical: true, cancellationToken));
    }

    public async Task<AppointmentDetails> DecideAsync(
        Guid userId,
        Guid appointmentId,
        bool approve,
        string? reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (!await ManagesAsync(userId, appointment, cancellationToken))
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        var now = clock.UtcNow;
        try
        {
            if (approve)
            {
                appointment.Approve(now);
                await EnsureVisitTokenAsync(appointment, cancellationToken);
            }
            else
            {
                appointment.Reject(reason, now);
            }
        }
        catch (DomainException exception) when (exception.Code == "approval_expired")
        {
            await appointments.SaveChangesAsync(null, cancellationToken);
            throw;
        }

        await appointments.SaveChangesAsync(
            Audit(userId, approve ? AuditActions.AppointmentApproved : AuditActions.AppointmentRejected, now, metadata),
            cancellationToken);
        logger.LogInformation("Appointment {Reference} {Decision}", appointment.Reference, approve ? "approved" : "rejected");
        return await MapAsync(appointment, includeClinical: true, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentDetails>> ListPracticeAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken,
        Guid? clinicId = null,
        bool assignedOnly = false)
    {
        EnsureRange(from, to);
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        var ownedClinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        if (clinicId is Guid requestedClinic)
        {
            await EnsurePracticeClinicAccessAsync(userId, doctor, ownedClinics, requestedClinic, cancellationToken);
        }

        var rows = new List<Domain.Appointments.Appointment>();
        if (doctor is not null)
        {
            rows.AddRange(await appointments.ListForDoctorAsync(doctor.Id, from, to, cancellationToken));
        }

        if (!assignedOnly && ownedClinics.Count > 0)
        {
            var clinicIds = clinicId is null
                ? ownedClinics.Select(clinic => clinic.Id).ToArray()
                : [clinicId.Value];
            rows.AddRange(await appointments.ListForClinicsAsync(clinicIds, from, to, cancellationToken));
        }

        if (!assignedOnly)
        {
            var staffClinicIds = await ResolveStaffClinicIdsAsync(userId, clinicId, cancellationToken);
            if (staffClinicIds.Count > 0)
            {
                rows.AddRange(await appointments.ListForClinicsAsync(staffClinicIds, from, to, cancellationToken));
            }
        }

        IReadOnlySet<Guid>? staffDoctorFilter = null;
        if (clinicId is Guid staffClinic && doctor is null && ownedClinics.All(item => item.Id != staffClinic))
        {
            var assigned = await clinicAccess.ListAssignedDoctorIdsAsync(userId, staffClinic, cancellationToken);
            if (assigned.Count > 0)
            {
                staffDoctorFilter = assigned.ToHashSet();
            }
        }

        var local = rows
            .Where(item => item.Status is not AppointmentStatus.Held and not AppointmentStatus.Expired)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .Where(item => InLocalRange(item, from, to))
            .Where(item => clinicId is null || item.ClinicId == clinicId.Value)
            .Where(item => !assignedOnly || doctor is null || item.DoctorId == doctor.Id)
            .Where(item => staffDoctorFilter is null || staffDoctorFilter.Contains(item.DoctorId))
            .OrderBy(item => item.StartsAtUtc)
            .ToArray();
        local = await BackfillMissingVisitTokensAsync(local, cancellationToken);
        return await MapAllAsync(local, includeClinical: true, cancellationToken);
    }

    private async Task<Domain.Appointments.Appointment[]> BackfillMissingVisitTokensAsync(
        Domain.Appointments.Appointment[] rows,
        CancellationToken cancellationToken)
    {
        var missingToken = false;
        foreach (var row in rows.Where(item =>
                     item.Status is AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed
                     && item.TokenNumber is null))
        {
            var tracked = await appointments.FindAsync(row.Id, tracking: true, cancellationToken);
            if (tracked is null || tracked.TokenNumber is not null)
            {
                continue;
            }

            await EnsureVisitTokenAsync(tracked, cancellationToken);
            missingToken = true;
        }

        if (!missingToken)
        {
            return rows;
        }

        await appointments.SaveChangesAsync(null, cancellationToken);
        var refreshed = new List<Domain.Appointments.Appointment>(rows.Length);
        foreach (var row in rows)
        {
            refreshed.Add(await appointments.FindAsync(row.Id, tracking: false, cancellationToken) ?? row);
        }

        return refreshed.ToArray();
    }

    /// <summary>
    /// Reception patient directory search. Uses appointment/patient stores directly and does not
    /// apply the calendar <see cref="EnsureRange"/> limit used by practice schedule listing.
    /// </summary>
    public async Task<IReadOnlyList<AppointmentDetails>> SearchClinicPatientVisitsAsync(
        Guid userId,
        Guid clinicId,
        Guid? branchId,
        string? query,
        Guid? accountUserId,
        CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        var ownedClinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        await EnsurePracticeClinicAccessAsync(userId, doctor, ownedClinics, clinicId, cancellationToken);

        var rows = await appointments.SearchClinicPatientVisitsAsync(
            clinicId,
            branchId,
            query,
            accountUserId,
            take: 500,
            cancellationToken);

        IReadOnlySet<Guid>? staffDoctorFilter = null;
        if (doctor is null && ownedClinics.All(item => item.Id != clinicId))
        {
            var assigned = await clinicAccess.ListAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
            if (assigned.Count > 0)
            {
                staffDoctorFilter = assigned.ToHashSet();
            }
        }

        if (staffDoctorFilter is not null)
        {
            rows = rows.Where(item => staffDoctorFilter.Contains(item.DoctorId)).ToArray();
        }

        return await MapAllAsync(rows, includeClinical: false, cancellationToken);
    }

    private async Task EnsurePracticeClinicAccessAsync(
        Guid userId,
        Doctor? doctor,
        IReadOnlyList<Clinic> ownedClinics,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        if (ownedClinics.Any(clinic => clinic.Id == clinicId))
        {
            return;
        }

        if (doctor is not null)
        {
            var affiliation = await providers.FindAffiliationAsync(doctor.Id, clinicId, cancellationToken);
            if (affiliation is not null)
            {
                return;
            }
        }

        if (await clinicAccess.HasClinicStaffAccessAsync(userId, clinicId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
    }

    private async Task<IReadOnlyList<Guid>> ResolveStaffClinicIdsAsync(
        Guid userId,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var memberships = await queueStore.ListStaffForUserAsync(userId, cancellationToken);
        var active = memberships.Where(item => item.IsActive).Select(item => item.ClinicId).Distinct();
        if (clinicId is Guid requested)
        {
            return active.Contains(requested) ? [requested] : [];
        }

        return active.ToArray();
    }

    public async Task<AppointmentDetails> WalkInAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        string patientName,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await BookableScheduleAsync(scheduleId, date, start, cancellationToken, AvailabilityHorizon.StaffExtended);
        if (scheduleResult.IsFailure)
        {
            throw ToDomainException(scheduleResult.Error!);
        }

        var schedule = scheduleResult.Value!;
        await EnsureStaffCanBookScheduleAsync(userId, schedule, cancellationToken);

        var slotResult = await RequireSlotAsync(scheduleId, date, start, cancellationToken, horizon: AvailabilityHorizon.StaffExtended);
        if (slotResult.IsFailure)
        {
            throw ToDomainException(slotResult.Error!);
        }

        var slot = slotResult.Value!;
        var now = clock.UtcNow;
        await EnsureNoDuplicateActiveVisitAsync(
            userId,
            schedule.DoctorId,
            date,
            exceptAppointmentId: null,
            cancellationToken);
        var visit = Appointment.WalkIn(
            userId,
            patientName,
            schedule.DoctorId,
            schedule.ClinicId!.Value,
            schedule.BranchId!.Value,
            schedule.Id,
            ToUtc(date, slot.Start, schedule.TimeZoneId),
            ToUtc(date, slot.End, schedule.TimeZoneId),
            schedule.TimeZoneId,
            schedule.ConsultationFee,
            now,
            schedule.BookingMode);
        await CaptureAppointmentLocationAsync(visit, cancellationToken);
        await appointments.AddHoldAsync(visit, cancellationToken);
        await EnsureVisitTokenAsync(visit, cancellationToken);
        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentWalkIn, now, metadata), cancellationToken);
        logger.LogInformation("Walk-in {Reference} confirmed", visit.Reference);
        return await MapAsync(visit, includeClinical: true, cancellationToken);
    }

    public async Task<AppointmentDetails> BookForPatientAsync(
        Guid staffUserId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        string patientName,
        string? phoneE164,
        string? reason,
        string? notes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await BookableScheduleAsync(scheduleId, date, start, cancellationToken, AvailabilityHorizon.StaffExtended);
        if (scheduleResult.IsFailure)
        {
            throw ToDomainException(scheduleResult.Error!);
        }

        var schedule = scheduleResult.Value!;
        await EnsureStaffCanBookScheduleAsync(staffUserId, schedule, cancellationToken);
        var slotResult = await RequireSlotAsync(scheduleId, date, start, cancellationToken, horizon: AvailabilityHorizon.StaffExtended);
        if (slotResult.IsFailure)
        {
            throw ToDomainException(slotResult.Error!);
        }

        var slot = slotResult.Value!;
        var now = clock.UtcNow;

        Guid accountUserId = staffUserId;
        if (!string.IsNullOrWhiteSpace(phoneE164))
        {
            var phone = PhoneNumber.ParseIndianMobile(phoneE164).E164;
            var existing = await auth.FindAccountByPhoneAsync(phone, cancellationToken);
            if (existing is null)
            {
                existing = await auth.CreatePatientAccountAsync(phone, [RoleNames.Patient], now, cancellationToken);
                await auth.AddAuditLogAsync(
                    AuditLog.Record(staffUserId, AuditActions.DeskPatientRegistered, now, metadata.IpAddress, metadata.UserAgent),
                    cancellationToken);
                await auth.SaveChangesAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(patientName))
            {
                await auth.UpdatePatientProfileAsync(existing.UserId, patientName.Trim(), Languages.English, now, cancellationToken);
                await auth.SaveChangesAsync(cancellationToken);
            }

            accountUserId = existing.UserId;
        }

        await EnsureNoDuplicateActiveVisitAsync(
            accountUserId,
            schedule.DoctorId,
            date,
            exceptAppointmentId: null,
            cancellationToken);

        var visit = Appointment.DeskBook(
            accountUserId,
            patientName,
            schedule.DoctorId,
            schedule.ClinicId!.Value,
            schedule.BranchId!.Value,
            schedule.Id,
            ToUtc(date, slot.Start, schedule.TimeZoneId),
            ToUtc(date, slot.End, schedule.TimeZoneId),
            schedule.TimeZoneId,
            schedule.ConsultationFee,
            reason,
            notes,
            now,
            schedule.BookingMode);
        await CaptureAppointmentLocationAsync(visit, cancellationToken);
        await appointments.AddHoldAsync(visit, cancellationToken);
        await EnsureVisitTokenAsync(visit, cancellationToken);
        await appointments.SaveChangesAsync(Audit(staffUserId, AuditActions.AppointmentDeskBooked, now, metadata), cancellationToken);
        logger.LogInformation("Desk booking {Reference} confirmed", visit.Reference);
        if (visit.Status == AppointmentStatus.Confirmed)
        {
            await PublishAppointmentAsync(AutomationTriggerTypes.AppointmentConfirmed, visit, cancellationToken);
        }

        return await MapAsync(visit, includeClinical: true, cancellationToken);
    }

    private async Task EnsureStaffCanBookScheduleAsync(
        Guid userId,
        Domain.Scheduling.ConsultationSchedule schedule,
        CancellationToken cancellationToken)
    {
        var clinicId = schedule.ClinicId
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        var branchId = schedule.BranchId
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");

        var owned = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        if (owned.Any(clinic => clinic.Id == clinicId))
        {
            return;
        }

        await clinicAccess.EnsureDoctorCoverageAsync(userId, clinicId, branchId, schedule.DoctorId, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSummary>> ListAdminAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        var rows = await appointments.ListForAdminAsync(from, to, cancellationToken);
        var mapped = await MapAllAsync(rows.Where(item => InLocalRange(item, from, to)).ToArray(), includeClinical: false, cancellationToken);
        return mapped.Select(item => new AppointmentSummary(
            item.Id,
            item.Reference,
            item.Status,
            item.DoctorId,
            item.DoctorName,
            item.ClinicId,
            item.ClinicName,
            item.StartsAtUtc,
            item.EndsAtUtc,
            item.ConsultationFee,
            item.Currency)).ToArray();
    }

    public async Task<IReadOnlyList<FamilyMemberDetails>> ListFamilyAsync(Guid userId, CancellationToken cancellationToken) =>
        (await appointments.ListFamilyAsync(userId, cancellationToken)).Select(MapFamily).ToArray();

    public async Task<FamilyMemberDetails> AddFamilyAsync(
        Guid userId,
        string fullName,
        DateOnly? dateOfBirth,
        string? gender,
        string relationship,
        CancellationToken cancellationToken)
    {
        if (dateOfBirth > DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
        {
            throw DomainException.Validation("date_of_birth_invalid", "Date of birth cannot be in the future.");
        }

        if (await appointments.CountFamilyAsync(userId, cancellationToken) >= 10)
        {
            throw DomainException.Validation("family_limit", "An account can keep up to 10 family profiles.");
        }

        var member = FamilyMember.Create(userId, fullName, dateOfBirth, gender, relationship, clock.UtcNow);
        appointments.AddFamily(member);
        await appointments.SaveChangesAsync(null, cancellationToken);
        return MapFamily(member);
    }

    public async Task RemoveFamilyAsync(Guid userId, Guid memberId, CancellationToken cancellationToken)
    {
        var member = await appointments.FindFamilyAsync(memberId, userId, cancellationToken)
            ?? throw DomainException.NotFound("family_member_not_found", "That family profile was not found.");
        appointments.RemoveFamily(member);
        await appointments.SaveChangesAsync(null, cancellationToken);
    }

    private async Task<AppResult<Domain.Scheduling.ConsultationSchedule>> BookableScheduleAsync(
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        CancellationToken cancellationToken,
        AvailabilityHorizon horizon = AvailabilityHorizon.PatientTwoWeeks)
    {
        var schedule = await schedules.FindAsync(scheduleId, tracking: false, cancellationToken);
        if (schedule is null || schedule.ClinicId is null || schedule.BranchId is null)
        {
            return AppResult<Domain.Scheduling.ConsultationSchedule>.NotFound(
                "schedule_not_found",
                "That schedule is not available.");
        }

        var slot = await RequireSlotAsync(scheduleId, date, start, cancellationToken, horizon: horizon);
        if (slot.IsFailure)
        {
            return AppResult<Domain.Scheduling.ConsultationSchedule>.Failure(slot.Error!);
        }

        return AppResult<Domain.Scheduling.ConsultationSchedule>.Success(schedule);
    }

    private async Task<AppResult<AvailableSlot>> RequireSlotAsync(
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null,
        AvailabilityHorizon horizon = AvailabilityHorizon.PatientTwoWeeks)
    {
        var availability = await scheduleService.GetAvailabilityAsync(
            scheduleId,
            date,
            cancellationToken,
            exceptAppointmentId,
            horizon);
        if (availability.IsFailure)
        {
            return AppResult<AvailableSlot>.Failure(availability.Error!);
        }

        var slot = availability.Value!.Slots.FirstOrDefault(item => item.Start == start && item.Available);
        if (slot is null)
        {
            return AppResult<AvailableSlot>.Conflict("slot_unavailable", "That time is no longer available.");
        }

        return AppResult<AvailableSlot>.Success(slot);
    }

    private async Task<string> PatientNameAsync(Guid userId, Guid? familyMemberId, CancellationToken cancellationToken)
    {
        if (familyMemberId is null)
        {
            var ownName = await appointments.FindPatientNameAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(ownName))
            {
                throw DomainException.Validation("patient_name_mandatory", "Patient name is mandatory.");
            }

            return ownName.Trim();
        }

        var member = await appointments.FindFamilyAsync(familyMemberId.Value, userId, cancellationToken)
            ?? throw DomainException.NotFound("family_member_not_found", "That family profile was not found.");
        if (string.IsNullOrWhiteSpace(member.FullName))
        {
            throw DomainException.Validation("patient_name_mandatory", "Patient name is mandatory.");
        }

        return member.FullName.Trim();
    }

    private async Task<Domain.Appointments.Appointment> MutableAsync(
        Guid userId,
        Guid appointmentId,
        bool practice,
        CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        var owns = appointment.AccountUserId == userId && !appointment.IsWalkIn;
        if (owns || (practice && await ManagesAsync(userId, appointment, cancellationToken)))
        {
            return appointment;
        }

        throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
    }

    private async Task<bool> ManagesAsync(Guid userId, Domain.Appointments.Appointment appointment, CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
        if (doctor?.UserId == userId)
        {
            return true;
        }

        var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
        if (clinic?.OwnerUserId == userId)
        {
            return true;
        }

        try
        {
            await clinicAccess.EnsureDoctorCoverageAsync(
                userId,
                appointment.ClinicId,
                appointment.BranchId,
                appointment.DoctorId,
                cancellationToken);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<AppointmentDetails>> MapAllAsync(
        IReadOnlyList<Domain.Appointments.Appointment> rows,
        bool includeClinical,
        CancellationToken cancellationToken)
    {
        var mapped = new List<AppointmentDetails>(rows.Count);
        foreach (var row in rows)
        {
            mapped.Add(await MapAsync(row, includeClinical, cancellationToken));
        }

        return mapped;
    }

    private async Task EnsureVisitTokenAsync(Domain.Appointments.Appointment appointment, CancellationToken cancellationToken)
    {
        if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed))
        {
            return;
        }

        if (appointment.TokenNumber is not null || !string.IsNullOrWhiteSpace(appointment.TokenLabel))
        {
            return;
        }

        var localStart = TimeZoneInfo.ConvertTime(appointment.StartsAtUtc, Zone(appointment.TimeZoneId));
        var operatingDate = DateOnly.FromDateTime(localStart.DateTime);
        var localTime = TimeOnly.FromDateTime(localStart.DateTime);
        string prefix;
        var rangeStartMinutes = QueueTokenCounter.DayWideRangeMinutes;

        if (appointment.BookingMode == ScheduleBookingMode.HourlyQueue)
        {
            var schedule = await schedules.FindAsync(appointment.ScheduleId, tracking: false, cancellationToken)
                ?? throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
            prefix = schedule.TokenPrefixForRange(operatingDate, localTime);
            rangeStartMinutes = localTime.Hour * 60 + localTime.Minute;
        }
        else
        {
            var settings = await queueStore.FindSettingsAsync(appointment.ClinicId, appointment.BranchId, cancellationToken);
            prefix = string.IsNullOrWhiteSpace(settings?.TokenPrefix)
                ? QueueDefaults.TokenPrefix
                : settings.TokenPrefix.Trim();
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var counter = await queueStore.FindCounterAsync(
                    appointment.ClinicId,
                    appointment.BranchId,
                    appointment.DoctorId,
                    operatingDate,
                    cancellationToken,
                    rangeStartMinutes);
                if (counter is null)
                {
                    counter = QueueTokenCounter.Start(
                        appointment.ClinicId,
                        appointment.BranchId,
                        appointment.DoctorId,
                        operatingDate,
                        rangeStartMinutes);
                    queueStore.AddCounter(counter);
                }

                var number = counter.Next();
                var label = $"{prefix}{number}";
                // Persist the counter first so a failed appointment save does not unlock the number for reuse,
                // and so a concurrency retry never sees an in-memory token that was never committed.
                await queueStore.SaveTokenIssueAsync(cancellationToken);
                appointment.AssignVisitToken(label, number, clock.UtcNow);
                return;
            }
            catch (DomainException exception) when (
                (exception.Code is "queue_conflict" or "concurrency_conflict") && attempt < 2)
            {
                // Concurrent token allocation — retry with a fresh counter read.
            }
        }

        throw DomainException.Conflict("token_issue_failed", "Could not issue a visit token. Try again.");
    }

    private async Task EnsureVisitTokenIfNeededAsync(Domain.Appointments.Appointment appointment, CancellationToken cancellationToken)
    {
        if (appointment.TokenNumber is not null || string.IsNullOrWhiteSpace(appointment.TokenLabel) is false)
        {
            return;
        }

        if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed))
        {
            return;
        }

        await EnsureVisitTokenAsync(appointment, cancellationToken);
        await appointments.SaveChangesAsync(null, cancellationToken);
    }

    private async Task CaptureAppointmentLocationAsync(
        Domain.Appointments.Appointment appointment,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolvePracticeLocationAsync(
            appointment.DoctorId,
            appointment.ClinicId,
            appointment.BranchId,
            cancellationToken);
        appointment.CaptureLocation(
            resolved.ClinicName,
            resolved.BranchName,
            resolved.FormattedAddress,
            resolved.Latitude,
            resolved.Longitude,
            resolved.GooglePlaceId,
            clock.UtcNow);
    }

    private async Task<(
        string ClinicName,
        string? BranchName,
        string FormattedAddress,
        decimal? Latitude,
        decimal? Longitude,
        string? GooglePlaceId)> ResolvePracticeLocationAsync(
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken);
        var branch = clinic?.Branches.FirstOrDefault(item => item.Id == branchId);
        var affiliations = await providers.ListAffiliationDetailsAsync(
            doctorId,
            approvedClinicsOnly: false,
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime),
            cancellationToken);
        var affiliation = affiliations.FirstOrDefault(item =>
            item.ClinicId == clinicId && (item.BranchId is null || item.BranchId == branchId));

        if (affiliation is { HasPracticeLocationOverride: true })
        {
            return (
                affiliation.ClinicName,
                affiliation.BranchName,
                affiliation.FormattedAddress ?? string.Empty,
                affiliation.Latitude,
                affiliation.Longitude,
                affiliation.GooglePlaceId);
        }

        var clinicName = clinic?.Name?.Trim() ?? "Clinic";
        var branchName = branch?.Name?.Trim();
        if (string.Equals(clinicName, branchName, StringComparison.OrdinalIgnoreCase))
        {
            branchName = null;
        }

        return (
            clinicName,
            branchName,
            branch?.FormattedAddress
                ?? PracticeLocation.FormatFull(
                    branch?.AddressLine,
                    branch?.AddressLine2,
                    branch?.Landmark,
                    branch?.Locality,
                    branch?.City,
                    branch?.District,
                    branch?.State,
                    branch?.PostalCode,
                    branch?.Country)
                ?? string.Empty,
            branch?.Latitude,
            branch?.Longitude,
            branch?.GooglePlaceId);
    }

    private async Task<AppointmentDetails> MapAsync(
        Domain.Appointments.Appointment appointment,
        bool includeClinical,
        CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
        var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
        var branch = clinic?.Branches.FirstOrDefault(item => item.Id == appointment.BranchId);

        var clinicName = !string.IsNullOrWhiteSpace(appointment.LocationClinicName)
            ? appointment.LocationClinicName.Trim()
            : clinic?.Name?.Trim() ?? "Clinic";
        var branchName = !string.IsNullOrWhiteSpace(appointment.LocationBranchName)
            ? appointment.LocationBranchName.Trim()
            : branch?.Name?.Trim() ?? string.Empty;
        if (string.Equals(clinicName, branchName, StringComparison.OrdinalIgnoreCase))
        {
            branchName = string.Empty;
        }

        var address = !string.IsNullOrWhiteSpace(appointment.LocationAddress)
            ? appointment.LocationAddress.Trim()
            : branch?.FormattedAddress
                ?? (branch is null
                    ? string.Empty
                    : PracticeLocation.FormatFull(
                        branch.AddressLine,
                        branch.AddressLine2,
                        branch.Landmark,
                        branch.Locality,
                        branch.City,
                        branch.District,
                        branch.State,
                        branch.PostalCode,
                        branch.Country));
        var latitude = appointment.LocationLatitude ?? branch?.Latitude;
        var longitude = appointment.LocationLongitude ?? branch?.Longitude;
        var googlePlaceId = appointment.LocationGooglePlaceId ?? branch?.GooglePlaceId;
        var link = new PlaceLinkInput(clinicName, address, latitude, longitude, googlePlaceId);

        var primarySpecializationId = doctor?.Specializations.FirstOrDefault(item => item.IsPrimary)?.SpecializationId
            ?? doctor?.Specializations.FirstOrDefault()?.SpecializationId;
        var specialization = primarySpecializationId is Guid specializationId
            ? SpecializationCatalog.All.FirstOrDefault(item => item.Id == specializationId)?.Name
            : null;
        var doctorName = FormatDoctorName(doctor?.DisplayName);

        return new AppointmentDetails(
            appointment.Id,
            appointment.Reference,
            appointment.Status.ToString(),
            appointment.PatientName,
            appointment.FamilyMemberId,
            appointment.AccountUserId == Guid.Empty ? null : appointment.AccountUserId,
            appointment.DoctorId,
            doctorName,
            specialization,
            doctor?.Status == VerificationStatus.Approved,
            appointment.ScheduleId,
            appointment.ClinicId,
            clinicName,
            appointment.BranchId,
            branchName,
            address,
            latitude,
            longitude,
            googlePlaceId,
            placeLinks.MapsUrl(link),
            placeLinks.DirectionsUrl(link),
            latitude is not null && longitude is not null,
            appointment.StartsAtUtc,
            appointment.EndsAtUtc,
            appointment.TimeZoneId,
            appointment.ConsultationFee,
            appointment.Currency,
            appointment.PaymentMethod,
            appointment.PaymentStatus,
            appointment.PaidAmount,
            appointment.RequiresApproval,
            appointment.IsWalkIn,
            appointment.HoldExpiresAtUtc,
            includeClinical ? appointment.Reason : null,
            includeClinical ? appointment.Notes : null,
            includeClinical ? appointment.DecisionNote : null,
            AppointmentPolicy.CancellationSummary,
            appointment.TokenLabel,
            appointment.TokenNumber,
            appointment.BookingMode == ScheduleBookingMode.HourlyQueue ? "HourlyQueue" : "TimedSlots",
            appointment.SameDayRescheduleCount,
            await CanSameDayRescheduleAsync(appointment, cancellationToken));
    }

    private static bool MeetsSameDayRescheduleBasics(Domain.Appointments.Appointment appointment, DateTimeOffset now)
    {
        if (appointment.Status != AppointmentStatus.Confirmed || appointment.StartsAtUtc > now)
        {
            return false;
        }

        if (appointment.SameDayRescheduleCount >= AppointmentPolicy.MaxSameDayReschedules)
        {
            return false;
        }

        var localDate = LocalOperatingDate(appointment.StartsAtUtc, appointment.TimeZoneId);
        var today = LocalOperatingDate(now, appointment.TimeZoneId);
        return localDate == today;
    }

    private async Task<bool> CanSameDayRescheduleAsync(
        Domain.Appointments.Appointment appointment,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (!MeetsSameDayRescheduleBasics(appointment, now))
        {
            return false;
        }

        var today = LocalOperatingDate(now, appointment.TimeZoneId);
        var availability = await scheduleService.GetAvailabilityAsync(
            appointment.ScheduleId,
            today,
            cancellationToken,
            appointment.Id,
            AvailabilityHorizon.StaffExtended);
        return availability.IsSuccess && availability.Value!.Slots.Any(slot => slot.Available);
    }

    private async Task EnsureSameDayRescheduleWindowAsync(
        Domain.Appointments.Appointment appointment,
        DateOnly targetDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var originalDate = LocalOperatingDate(appointment.StartsAtUtc, appointment.TimeZoneId);
        var today = LocalOperatingDate(now, appointment.TimeZoneId);
        if (originalDate != today || targetDate != today)
        {
            throw DomainException.Validation(
                "same_day_only",
                "Missed visits can only be moved to another slot on the same day.");
        }

        if (appointment.SameDayRescheduleCount >= AppointmentPolicy.MaxSameDayReschedules)
        {
            throw DomainException.Conflict(
                "reschedule_limit",
                $"You can reschedule a missed visit at most {AppointmentPolicy.MaxSameDayReschedules} times on the same day.");
        }

        if (!await CanSameDayRescheduleAsync(appointment, cancellationToken))
        {
            throw DomainException.Conflict(
                "clinic_closed",
                "Same-day rescheduling is no longer available — the clinic has no remaining open slots today.");
        }
    }

    private static string FormatDoctorName(string? displayName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "Doctor" : displayName.Trim();
        if (name.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"Dr. {name}";
    }

    private async Task EnsureNoDuplicateActiveVisitAsync(
        Guid accountUserId,
        Guid doctorId,
        DateOnly localDate,
        Guid? exceptAppointmentId,
        CancellationToken cancellationToken)
    {
        if (await FindDuplicateActiveVisitErrorAsync(
                accountUserId,
                doctorId,
                localDate,
                exceptAppointmentId,
                cancellationToken) is { } error)
        {
            throw DomainException.Conflict(error.Code, error.Message);
        }
    }

    private async Task<AppError?> FindDuplicateActiveVisitErrorAsync(
        Guid accountUserId,
        Guid doctorId,
        DateOnly localDate,
        Guid? exceptAppointmentId,
        CancellationToken cancellationToken)
    {
        var candidates = await appointments.ListActiveForDuplicateCheckAsync(
            accountUserId,
            doctorId,
            localDate,
            clock.UtcNow,
            exceptAppointmentId,
            cancellationToken);
        return candidates.FirstOrDefault() is null
            ? null
            : AppError.Conflict(
                "duplicate_appointment",
                "You already have an appointment with this doctor on this date. Please select another date.");
    }

    private static DateOnly LocalOperatingDate(DateTimeOffset instant, string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone(timeZoneId)).DateTime);

    private static bool InLocalRange(Domain.Appointments.Appointment appointment, DateOnly from, DateOnly to)
    {
        var local = TimeZoneInfo.ConvertTime(appointment.StartsAtUtc, Zone(appointment.TimeZoneId)).DateTime;
        var date = DateOnly.FromDateTime(local);
        return date >= from && date <= to;
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone(timeZoneId)), TimeSpan.Zero);
    }

    private static TimeZoneInfo Zone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;

    private async Task PublishAppointmentAsync(
        string eventType,
        Appointment appointment,
        CancellationToken cancellationToken,
        DateTimeOffset? previousStartsAtUtc = null,
        DateTimeOffset? previousEndsAtUtc = null)
    {
        try
        {
            var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
            var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
            var doctorName = !string.IsNullOrWhiteSpace(doctor?.DisplayName)
                ? doctor!.DisplayName!
                : $"Dr. {doctor?.FirstName} {doctor?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(doctorName) || doctorName == "Dr.")
            {
                doctorName = "Doctor";
            }

            await automation.PublishAsync(
                eventType,
                new
                {
                    appointmentId = appointment.Id,
                    accountUserId = appointment.AccountUserId,
                    userId = appointment.AccountUserId,
                    patientName = appointment.PatientName,
                    doctorName,
                    clinicName = clinic?.Name ?? "Clinic",
                    reference = appointment.Reference,
                    tokenLabel = appointment.TokenLabel,
                    status = appointment.Status.ToString(),
                    startsAtUtc = appointment.StartsAtUtc,
                    endsAtUtc = appointment.EndsAtUtc,
                    previousStartsAtUtc,
                    previousEndsAtUtc,
                    timeZoneId = appointment.TimeZoneId,
                    bookingMode = appointment.BookingMode.ToString(),
                    cancellationReason = appointment.DecisionNote,
                    clinicId = appointment.ClinicId,
                    branchId = appointment.BranchId,
                    doctorId = appointment.DoctorId,
                    organizationId = clinic?.OrganizationId
                },
                $"{eventType}:{appointment.Id:N}:{appointment.UpdatedAtUtc.UtcTicks}",
                cancellationToken,
                clinic?.OrganizationId,
                appointment.Id,
                appointment.AccountUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish automation event {EventType} for appointment {AppointmentId}", eventType, appointment.Id);
        }
    }

    private static void EnsureRange(DateOnly from, DateOnly to)
    {
        if (DateValidation.ValidateInclusiveRange(from, to) is { } error)
        {
            throw DomainException.Validation(error.Code, error.Message);
        }
    }

    private static AppError? ValidateRange(DateOnly from, DateOnly to) =>
        DateValidation.ValidateInclusiveRange(from, to);

    /// <summary>
    /// Bridge for staff booking paths not yet fully migrated to <see cref="AppResult"/>.
    /// Prefer returning <see cref="AppResult{T}"/> from public APIs instead of calling this.
    /// </summary>
    private static DomainException ToDomainException(AppError error) =>
        error.StatusCode switch
        {
            401 => DomainException.Unauthorized(error.Code, error.Message),
            403 => DomainException.Forbidden(error.Code, error.Message),
            404 => DomainException.NotFound(error.Code, error.Message),
            409 => DomainException.Conflict(error.Code, error.Message),
            422 => DomainException.Unprocessable(error.Code, error.Message),
            429 => DomainException.TooManyRequests(error.Code, error.Message),
            _ => DomainException.Validation(error.Code, error.Message)
        };

    private static AuditLog Audit(Guid userId, string action, DateTimeOffset now, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, now, metadata.IpAddress, metadata.UserAgent);

    private static FamilyMemberDetails MapFamily(FamilyMember member) =>
        new(member.Id, member.FullName, member.DateOfBirth, member.Gender, member.Relationship);
}
