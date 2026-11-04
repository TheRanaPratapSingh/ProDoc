using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Providers;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Appointments;

public sealed class BookingService(
    IAppointmentStore appointments,
    IScheduleStore schedules,
    ScheduleService scheduleService,
    IProviderStore providers,
    IAutomationEventBus automation,
    IClock clock,
    ILogger<BookingService> logger)
{
    public async Task<AppointmentDetails> HoldAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await BookableScheduleAsync(scheduleId, date, start, cancellationToken);
        var slot = await RequireSlotAsync(scheduleId, date, start, cancellationToken);
        var starts = ToUtc(date, slot.Start, schedule.TimeZoneId);
        var ends = ToUtc(date, slot.End, schedule.TimeZoneId);
        var now = clock.UtcNow;
        if (schedule.RequiresApproval && starts <= now.AddMinutes(30))
        {
            throw DomainException.Validation("approval_window_closed", "This visit is too soon for an approval request.");
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
            now);
        await appointments.AddHoldAsync(held, cancellationToken);
        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentHeld, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {AppointmentId} held", held.Id);
        return await MapAsync(held, includeClinical: true, cancellationToken);
    }

    public async Task<AppointmentDetails> ConfirmAsync(
        Guid userId,
        Guid appointmentId,
        Guid? familyMemberId,
        string? reason,
        string? notes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        var now = clock.UtcNow;
        var patientName = await PatientNameAsync(userId, familyMemberId, cancellationToken);
        try
        {
            appointment.Confirm(patientName, familyMemberId, reason, notes, now);
        }
        catch (DomainException exception) when (exception.Code == "hold_expired")
        {
            await appointments.SaveChangesAsync(null, cancellationToken);
            throw;
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

        return await MapAsync(appointment, includeClinical: true, cancellationToken);
    }

    public async Task ReleaseAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        appointment.Release(clock.UtcNow);
        await appointments.SaveChangesAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentDetails>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await appointments.ListForAccountAsync(userId, cancellationToken);
        return await MapAllAsync(rows, includeClinical: true, cancellationToken);
    }

    public async Task<AppointmentDetails> GetAsync(Guid userId, Guid appointmentId, bool practice, CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        var owns = appointment.AccountUserId == userId && !appointment.IsWalkIn;
        var manages = practice && await ManagesAsync(userId, appointment, cancellationToken);
        if (!owns && !manages)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        return await MapAsync(appointment, includeClinical: true, cancellationToken);
    }

    public async Task<AppointmentDetails> CancelAsync(
        Guid userId,
        Guid appointmentId,
        string? reason,
        bool practice,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await MutableAsync(userId, appointmentId, practice, cancellationToken);
        var now = clock.UtcNow;
        appointment.Cancel(staff: practice && await ManagesAsync(userId, appointment, cancellationToken), reason, now);
        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentCancelled, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {Reference} cancelled", appointment.Reference);
        await PublishAppointmentAsync(AutomationTriggerTypes.AppointmentCancelled, appointment, cancellationToken);
        return await MapAsync(appointment, includeClinical: true, cancellationToken);
    }

    public async Task<AppointmentDetails> RescheduleAsync(
        Guid userId,
        Guid appointmentId,
        DateOnly date,
        TimeOnly start,
        bool practice,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await MutableAsync(userId, appointmentId, practice, cancellationToken);
        var slot = await RequireSlotAsync(appointment.ScheduleId, date, start, cancellationToken);
        var schedule = await schedules.FindAsync(appointment.ScheduleId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
        var starts = ToUtc(date, slot.Start, schedule.TimeZoneId);
        var ends = ToUtc(date, slot.End, schedule.TimeZoneId);
        var now = clock.UtcNow;
        var staff = practice && await ManagesAsync(userId, appointment, cancellationToken);
        appointment.Reschedule(starts, ends, staff, now);
        await appointments.SaveMoveAsync(appointment, Audit(userId, AuditActions.AppointmentRescheduled, now, metadata), cancellationToken);
        logger.LogInformation("Appointment {Reference} rescheduled", appointment.Reference);
        return await MapAsync(appointment, includeClinical: true, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        var clinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        var rows = new List<Domain.Appointments.Appointment>();
        if (doctor is not null)
        {
            rows.AddRange(await appointments.ListForDoctorAsync(doctor.Id, from, to, cancellationToken));
        }

        if (clinics.Count > 0)
        {
            rows.AddRange(await appointments.ListForClinicsAsync(clinics.Select(clinic => clinic.Id).ToArray(), from, to, cancellationToken));
        }

        var local = rows
            .Where(item => item.Status is not AppointmentStatus.Held and not AppointmentStatus.Expired)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .Where(item => InLocalRange(item, from, to))
            .OrderBy(item => item.StartsAtUtc)
            .ToArray();
        return await MapAllAsync(local, includeClinical: true, cancellationToken);
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
        var schedule = await BookableScheduleAsync(scheduleId, date, start, cancellationToken);
        var clinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        if (clinics.All(clinic => clinic.Id != schedule.ClinicId))
        {
            throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        }

        var slot = await RequireSlotAsync(scheduleId, date, start, cancellationToken);
        var now = clock.UtcNow;
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
            now);
        await appointments.AddHoldAsync(visit, cancellationToken);
        await appointments.SaveChangesAsync(Audit(userId, AuditActions.AppointmentWalkIn, now, metadata), cancellationToken);
        logger.LogInformation("Walk-in {Reference} confirmed", visit.Reference);
        return await MapAsync(visit, includeClinical: true, cancellationToken);
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

    private async Task<Domain.Scheduling.ConsultationSchedule> BookableScheduleAsync(
        Guid scheduleId,
        DateOnly date,
        TimeOnly start,
        CancellationToken cancellationToken)
    {
        var schedule = await schedules.FindAsync(scheduleId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
        if (schedule.ClinicId is null || schedule.BranchId is null)
        {
            throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
        }

        await RequireSlotAsync(scheduleId, date, start, cancellationToken);
        return schedule;
    }

    private async Task<AvailableSlot> RequireSlotAsync(Guid scheduleId, DateOnly date, TimeOnly start, CancellationToken cancellationToken)
    {
        var availability = await scheduleService.GetAvailabilityAsync(scheduleId, date, cancellationToken);
        return availability.Slots.FirstOrDefault(slot => slot.Start == start)
            ?? throw DomainException.Conflict("slot_unavailable", "That time is no longer available.");
    }

    private async Task<string> PatientNameAsync(Guid userId, Guid? familyMemberId, CancellationToken cancellationToken)
    {
        if (familyMemberId is null)
        {
            return await appointments.FindPatientNameAsync(userId, cancellationToken)
                ?? throw DomainException.Validation("name_invalid", "Add your name to the account before booking.");
        }

        var member = await appointments.FindFamilyAsync(familyMemberId.Value, userId, cancellationToken)
            ?? throw DomainException.NotFound("family_member_not_found", "That family profile was not found.");
        return member.FullName;
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
        return clinic?.OwnerUserId == userId;
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

    private async Task<AppointmentDetails> MapAsync(
        Domain.Appointments.Appointment appointment,
        bool includeClinical,
        CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
        var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
        var branch = clinic?.Branches.FirstOrDefault(item => item.Id == appointment.BranchId);
        var address = branch is null ? string.Empty : string.Join(", ", new[] { branch.AddressLine, branch.City, branch.State, branch.PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new AppointmentDetails(
            appointment.Id,
            appointment.Reference,
            appointment.Status.ToString(),
            appointment.PatientName,
            appointment.FamilyMemberId,
            appointment.DoctorId,
            doctor?.DisplayName ?? "Doctor",
            appointment.ClinicId,
            clinic?.Name ?? "Clinic",
            appointment.BranchId,
            branch?.Name ?? "Branch",
            address,
            branch?.Latitude,
            branch?.Longitude,
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
            AppointmentPolicy.CancellationSummary);
    }

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

    private async Task PublishAppointmentAsync(string eventType, Appointment appointment, CancellationToken cancellationToken)
    {
        try
        {
            var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
            await automation.PublishAsync(
                eventType,
                new
                {
                    appointmentId = appointment.Id,
                    accountUserId = appointment.AccountUserId,
                    userId = appointment.AccountUserId,
                    status = appointment.Status.ToString(),
                    startsAtUtc = appointment.StartsAtUtc,
                    clinicId = appointment.ClinicId,
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
        if (to < from || to.DayNumber - from.DayNumber > 31)
        {
            throw DomainException.Validation("date_invalid", "Choose a range of 31 days or fewer.");
        }
    }

    private static AuditLog Audit(Guid userId, string action, DateTimeOffset now, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, now, metadata.IpAddress, metadata.UserAgent);

    private static FamilyMemberDetails MapFamily(FamilyMember member) =>
        new(member.Id, member.FullName, member.DateOfBirth, member.Gender, member.Relationship);
}
