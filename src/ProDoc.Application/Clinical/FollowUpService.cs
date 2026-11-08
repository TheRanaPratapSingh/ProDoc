using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Notifications;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Clinical;

public sealed class FollowUpService(
    IFollowUpStore store,
    IClinicAccess clinicAccess,
    IAutomationEventBus automation,
    IAutomationStore automationStore,
    NotificationService notifications,
    IOptions<FollowUpOptions> options,
    IClock clock,
    ILogger<FollowUpService> logger)
{
    public async Task<FollowUpDetails> CreateFromConsultationAsync(
        Guid userId,
        Guid consultationId,
        DateOnly recommendedDate,
        string? note,
        bool requiresBooking,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var consultation = await store.FindConsultationAsync(consultationId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        if (consultation.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        }

        var existing = await store.FindOpenForSourceConsultationAsync(consultationId, cancellationToken);
        if (existing is not null)
        {
            return await UpdateAsync(
                userId,
                existing.Id,
                recommendedDate,
                note,
                requiresBooking,
                metadata,
                cancellationToken);
        }

        return await CreateCoreAsync(
            userId,
            doctor,
            consultation.AccountUserId,
            consultation.FamilyMemberId,
            consultation.PatientName,
            consultation.ClinicId,
            consultation.BranchId,
            consultation.AppointmentId,
            consultation.Id,
            recommendedDate,
            note,
            requiresBooking,
            metadata,
            cancellationToken);
    }

    public async Task<FollowUpDetails> CreateAsync(
        Guid userId,
        Guid? sourceAppointmentId,
        Guid? sourceConsultationId,
        Guid patientUserId,
        Guid? familyMemberId,
        string patientName,
        Guid clinicId,
        Guid branchId,
        DateOnly recommendedDate,
        string? note,
        bool requiresBooking,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        if (sourceConsultationId is Guid consultationId)
        {
            return await CreateFromConsultationAsync(
                userId,
                consultationId,
                recommendedDate,
                note,
                requiresBooking,
                metadata,
                cancellationToken);
        }

        if (sourceAppointmentId is Guid appointmentId)
        {
            var appointment = await store.FindAppointmentAsync(appointmentId, tracking: false, cancellationToken)
                ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            if (appointment.DoctorId != doctor.Id)
            {
                throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            }

            patientUserId = appointment.AccountUserId;
            familyMemberId = appointment.FamilyMemberId;
            patientName = appointment.PatientName;
            clinicId = appointment.ClinicId;
            branchId = appointment.BranchId;
        }

        return await CreateCoreAsync(
            userId,
            doctor,
            patientUserId,
            familyMemberId,
            patientName,
            clinicId,
            branchId,
            sourceAppointmentId,
            null,
            recommendedDate,
            note,
            requiresBooking,
            metadata,
            cancellationToken);
    }

    public async Task<FollowUpDetails> UpdateAsync(
        Guid userId,
        Guid followUpId,
        DateOnly recommendedDate,
        string? note,
        bool? requiresBooking,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var followUp = await store.FindAsync(followUpId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        if (followUp.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        }

        var today = TodayLocal(followUp.TimeZoneId);
        var dateChanged = followUp.RecommendedDate != recommendedDate;
        followUp.Update(recommendedDate, today, note, requiresBooking, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.FollowUpUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        if (dateChanged)
        {
            await RescheduleRemindersAsync(followUp, cancellationToken);
        }

        await NotifyPatientRecommendationAsync(followUp, updated: true, cancellationToken);
        return await MapAsync(followUp, cancellationToken);
    }

    public async Task<FollowUpDetails> CancelAsync(
        Guid userId,
        Guid followUpId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var followUp = await store.FindAsync(followUpId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        if (followUp.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        }

        followUp.Cancel(clock.UtcNow);
        await store.CancelPendingRemindersAsync(followUp.Id, "Follow-up cancelled", clock.UtcNow, cancellationToken);
        await automationStore.CancelPendingRemindersForSubjectAsync(
            followUp.Id,
            AutomationTriggerTypes.FollowUpReminderDue,
            "Follow-up cancelled",
            clock.UtcNow,
            cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.FollowUpCancelled, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        await notifications.NotifyUsersAsync(
            [followUp.PatientUserId],
            NotificationKinds.FollowUpCancelled,
            followUp.Id,
            "Follow-up Cancelled",
            $"Your recommended follow-up with {await DoctorNameAsync(followUp.DoctorId, cancellationToken)} was cancelled.",
            essential: false,
            cancellationToken);

        return await MapAsync(followUp, cancellationToken);
    }

    public async Task<FollowUpDetails> CompleteAsync(
        Guid userId,
        Guid followUpId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var followUp = await store.FindAsync(followUpId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        if (followUp.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        }

        followUp.MarkCompleted(clock.UtcNow);
        await store.CancelPendingRemindersAsync(followUp.Id, "Follow-up completed", clock.UtcNow, cancellationToken);
        await automationStore.CancelPendingRemindersForSubjectAsync(
            followUp.Id,
            AutomationTriggerTypes.FollowUpReminderDue,
            "Follow-up completed",
            clock.UtcNow,
            cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.FollowUpCompleted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(followUp, cancellationToken);
    }

    public async Task LinkAppointmentAsync(
        Guid userId,
        Guid followUpId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var followUp = await store.FindAsync(followUpId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        if (followUp.PatientUserId != userId)
        {
            throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
        }

        var appointment = await store.FindAppointmentAsync(appointmentId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        if (appointment.DoctorId != followUp.DoctorId)
        {
            throw DomainException.Validation(
                "follow_up_doctor_mismatch",
                "Book the follow-up with the same doctor who recommended it.");
        }

        followUp.LinkAppointment(appointmentId, clock.UtcNow);
        await SuppressBookingNudgesAsync(followUp, cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.FollowUpLinked, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Follow-up {FollowUpId} linked to appointment {AppointmentId}", followUpId, appointmentId);
    }

    public async Task TryLinkAfterBookingAsync(
        Guid patientUserId,
        Guid? followUpId,
        Guid appointmentId,
        Guid doctorId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (followUpId is null || followUpId == Guid.Empty)
        {
            return;
        }

        try
        {
            var followUp = await store.FindAsync(followUpId.Value, tracking: true, cancellationToken);
            if (followUp is null
                || followUp.PatientUserId != patientUserId
                || followUp.DoctorId != doctorId
                || !followUp.IsOpen)
            {
                return;
            }

            followUp.LinkAppointment(appointmentId, clock.UtcNow);
            await SuppressBookingNudgesAsync(followUp, cancellationToken);
            await store.AddAuditAsync(Audit(patientUserId, AuditActions.FollowUpLinked, metadata), cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to link appointment {AppointmentId} to follow-up {FollowUpId}", appointmentId, followUpId);
        }
    }

    public Task<FollowUpDetails> GetForDoctorAsync(Guid userId, Guid followUpId, CancellationToken cancellationToken) =>
        GetAuthorizedAsync(userId, followUpId, asDoctor: true, asPatient: false, asReception: false, cancellationToken);

    public Task<FollowUpDetails> GetForPatientAsync(Guid userId, Guid followUpId, CancellationToken cancellationToken) =>
        GetAuthorizedAsync(userId, followUpId, asDoctor: false, asPatient: true, asReception: false, cancellationToken);

    public async Task<IReadOnlyList<FollowUpDetails>> ListForDoctorAsync(
        Guid userId,
        FollowUpListFilter filter,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireDoctorAsync(userId, cancellationToken);
        var rows = await store.ListForDoctorAsync(
            doctor.Id,
            filter.From,
            filter.To,
            filter.PatientQuery,
            filter.Status,
            filter.ClinicId,
            Math.Clamp(filter.Take, 1, 200),
            cancellationToken);
        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUpDetails>> ListForPatientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.ListForPatientAsync(userId, cancellationToken);
        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUpDetails>> ListForReceptionAsync(
        Guid userId,
        FollowUpListFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.ClinicId is not Guid clinicId || clinicId == Guid.Empty)
        {
            throw DomainException.Validation("clinic_required", "Choose a clinic to view follow-ups.");
        }

        await clinicAccess.EnsureClinicStaffAccessAsync(userId, clinicId, cancellationToken);
        var assignedDoctors = await clinicAccess.ListAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
        var doctorIds = filter.DoctorId is Guid doctorId
            ? assignedDoctors.Count == 0 || assignedDoctors.Contains(doctorId) ? new[] { doctorId } : Array.Empty<Guid>()
            : assignedDoctors.ToArray();

        var rows = await store.ListForReceptionAsync(
            doctorIds,
            [clinicId],
            filter.From,
            filter.To,
            filter.PatientQuery,
            filter.Status,
            Math.Clamp(filter.Take, 1, 200),
            cancellationToken);
        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task DeliverReminderAsync(
        Guid followUpId,
        string reminderLabel,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var followUp = await store.FindAsync(followUpId, tracking: false, cancellationToken);
        var reminder = await store.FindReminderByKeyAsync(idempotencyKey, tracking: true, cancellationToken);

        if (followUp is null || !followUp.IsOpen)
        {
            reminder?.Cancel("Follow-up closed", clock.UtcNow);
            if (reminder is not null)
            {
                await store.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // After booking, skip nudges that ask the patient to plan/book again.
        if (followUp.LinkedAppointmentId is not null
            && reminderLabel is "3d" or "1d" or "0d")
        {
            // Still send day-of awareness, but with booked wording handled below.
        }

        var doctorName = await DoctorNameAsync(followUp.DoctorId, cancellationToken);
        var dateLabel = followUp.RecommendedDate.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-IN"));
        var (title, body) = BuildReminderCopy(reminderLabel, doctorName, dateLabel, followUp.LinkedAppointmentId is not null);

        await notifications.NotifyUsersAsync(
            [followUp.PatientUserId],
            NotificationKinds.FollowUpReminder,
            followUp.Id,
            title,
            body,
            essential: false,
            cancellationToken);

        if (reminder is not null)
        {
            reminder.MarkSent(clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<FollowUpDetails> CreateCoreAsync(
        Guid userId,
        Doctor doctor,
        Guid patientUserId,
        Guid? familyMemberId,
        string patientName,
        Guid clinicId,
        Guid branchId,
        Guid? sourceAppointmentId,
        Guid? sourceConsultationId,
        DateOnly recommendedDate,
        string? note,
        bool requiresBooking,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var today = TodayLocal(ConsultationSchedule.DefaultTimeZone);
        var followUp = PatientFollowUp.Create(
            patientUserId,
            doctor.Id,
            clinicId,
            branchId,
            userId,
            patientName,
            recommendedDate,
            today,
            clock.UtcNow,
            familyMemberId,
            sourceAppointmentId,
            sourceConsultationId,
            note,
            requiresBooking,
            ConsultationSchedule.DefaultTimeZone);

        store.Add(followUp);
        await ScheduleRemindersAsync(followUp, cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.FollowUpCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        await NotifyPatientRecommendationAsync(followUp, updated: false, cancellationToken);
        return await MapAsync(followUp, cancellationToken);
    }

    private async Task ScheduleRemindersAsync(PatientFollowUp followUp, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        var days = (cfg.ReminderDaysBefore ?? [3, 1, 0])
            .Where(d => d >= 0)
            .Distinct()
            .OrderByDescending(d => d)
            .ToArray();
        var hour = Math.Clamp(cfg.ReminderLocalHour, 0, 23);
        var zone = Zone(followUp.TimeZoneId);

        foreach (var daysBefore in days)
        {
            var localDate = followUp.RecommendedDate.AddDays(-daysBefore);
            var localDateTime = localDate.ToDateTime(new TimeOnly(hour, 0));
            var availableAt = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), zone);
            if (availableAt <= clock.UtcNow.AddMinutes(1))
            {
                continue;
            }

            var type = FollowUpReminderLabels.FromDaysBefore(daysBefore);
            var label = FollowUpReminderLabels.ForType(type);
            if (type == FollowUpReminderType.Custom)
            {
                label = $"{daysBefore}d";
            }

            var key = $"followup:{followUp.Id:N}:reminder:{label}:{followUp.RecommendedDate:yyyyMMdd}";
            var reminder = FollowUpReminder.Schedule(followUp.Id, type, availableAt, key, clock.UtcNow);
            store.AddReminder(reminder);

            await automation.PublishAsync(
                AutomationTriggerTypes.FollowUpReminderDue,
                new
                {
                    followUpId = followUp.Id,
                    patientUserId = followUp.PatientUserId,
                    doctorId = followUp.DoctorId,
                    clinicId = followUp.ClinicId,
                    recommendedDate = followUp.RecommendedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    reminderLabel = label,
                    requiresBooking = followUp.RequiresBooking,
                    linkedAppointmentId = followUp.LinkedAppointmentId
                },
                key,
                cancellationToken,
                organizationId: null,
                subjectId: followUp.Id,
                userId: followUp.PatientUserId,
                availableAtUtc: availableAt);
        }
    }

    private async Task RescheduleRemindersAsync(PatientFollowUp followUp, CancellationToken cancellationToken)
    {
        await store.CancelPendingRemindersAsync(followUp.Id, "Follow-up date changed", clock.UtcNow, cancellationToken);
        await automationStore.CancelPendingRemindersForSubjectAsync(
            followUp.Id,
            AutomationTriggerTypes.FollowUpReminderDue,
            "Follow-up date changed",
            clock.UtcNow,
            cancellationToken);
        await ScheduleRemindersAsync(followUp, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task SuppressBookingNudgesAsync(PatientFollowUp followUp, CancellationToken cancellationToken)
    {
        // Keep due-date reminder; cancel earlier "plan your visit" nudges once booked.
        var reminders = await store.ListRemindersAsync(followUp.Id, cancellationToken);
        foreach (var reminder in reminders.Where(item =>
                     item.DeliveryStatus == FollowUpReminderDeliveryStatus.Pending
                     && item.ReminderType is FollowUpReminderType.ThreeDaysBefore or FollowUpReminderType.OneDayBefore))
        {
            reminder.Cancel("Appointment already booked", clock.UtcNow);
        }

        await automationStore.CancelPendingRemindersForSubjectAsync(
            followUp.Id,
            AutomationTriggerTypes.FollowUpReminderDue,
            "Appointment already booked",
            clock.UtcNow,
            cancellationToken);

        // Re-schedule only the due-date reminder if still pending/future.
        var dueKeyPrefix = $"followup:{followUp.Id:N}:reminder:0d:";
        var dueStillPending = reminders.Any(item =>
            item.IdempotencyKey.StartsWith(dueKeyPrefix, StringComparison.OrdinalIgnoreCase)
            && item.DeliveryStatus == FollowUpReminderDeliveryStatus.Pending
            && item.ScheduledAtUtc > clock.UtcNow);
        if (!dueStillPending && followUp.RecommendedDate >= TodayLocal(followUp.TimeZoneId))
        {
            var cfg = options.Value;
            var hour = Math.Clamp(cfg.ReminderLocalHour, 0, 23);
            var zone = Zone(followUp.TimeZoneId);
            var localDateTime = followUp.RecommendedDate.ToDateTime(new TimeOnly(hour, 0));
            var availableAt = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), zone);
            if (availableAt > clock.UtcNow.AddMinutes(1))
            {
                var key = $"followup:{followUp.Id:N}:reminder:0d:{followUp.RecommendedDate:yyyyMMdd}:booked";
                store.AddReminder(FollowUpReminder.Schedule(
                    followUp.Id,
                    FollowUpReminderType.OnDueDate,
                    availableAt,
                    key,
                    clock.UtcNow));
                await automation.PublishAsync(
                    AutomationTriggerTypes.FollowUpReminderDue,
                    new
                    {
                        followUpId = followUp.Id,
                        patientUserId = followUp.PatientUserId,
                        doctorId = followUp.DoctorId,
                        clinicId = followUp.ClinicId,
                        recommendedDate = followUp.RecommendedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        reminderLabel = "0d",
                        requiresBooking = followUp.RequiresBooking,
                        linkedAppointmentId = followUp.LinkedAppointmentId
                    },
                    key,
                    cancellationToken,
                    organizationId: null,
                    subjectId: followUp.Id,
                    userId: followUp.PatientUserId,
                    availableAtUtc: availableAt);
            }
        }
    }

    private async Task NotifyPatientRecommendationAsync(
        PatientFollowUp followUp,
        bool updated,
        CancellationToken cancellationToken)
    {
        var doctorName = await DoctorNameAsync(followUp.DoctorId, cancellationToken);
        var dateLabel = followUp.RecommendedDate.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-IN"));
        await notifications.NotifyUsersAsync(
            [followUp.PatientUserId],
            updated ? NotificationKinds.FollowUpUpdated : NotificationKinds.FollowUpRecommended,
            followUp.Id,
            updated ? "Follow-up Updated" : "Follow-up Recommended",
            updated
                ? $"{doctorName} updated your follow-up recommendation to {dateLabel}."
                : $"{doctorName} recommended a follow-up on {dateLabel}. Open ProDoc to review and book if needed.",
            essential: false,
            cancellationToken);
    }

    private static (string Title, string Body) BuildReminderCopy(
        string reminderLabel,
        string doctorName,
        string dateLabel,
        bool alreadyBooked)
    {
        if (alreadyBooked)
        {
            return reminderLabel switch
            {
                "0d" => ("Follow-up Due Today",
                    $"Your follow-up visit with {doctorName} is due today."),
                "1d" => ("Follow-up Tomorrow",
                    $"Reminder: Your follow-up visit with {doctorName} is tomorrow."),
                _ => ("Follow-up Reminder",
                    $"Your follow-up visit with {doctorName} is coming up on {dateLabel}.")
            };
        }

        return reminderLabel switch
        {
            "0d" => ("Follow-up Due Today",
                $"Your recommended follow-up with {doctorName} is due today."),
            "1d" => ("Follow-up Tomorrow",
                $"Reminder: Your recommended follow-up with {doctorName} is tomorrow."),
            _ => ("Follow-up Reminder",
                $"Your follow-up with {doctorName} is scheduled for {dateLabel}. Please plan your visit.")
        };
    }

    private async Task<FollowUpDetails> GetAuthorizedAsync(
        Guid userId,
        Guid followUpId,
        bool asDoctor,
        bool asPatient,
        bool asReception,
        CancellationToken cancellationToken)
    {
        var followUp = await store.FindAsync(followUpId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");

        if (asPatient && followUp.PatientUserId == userId)
        {
            return await MapAsync(followUp, cancellationToken);
        }

        if (asDoctor)
        {
            var doctor = await RequireDoctorAsync(userId, cancellationToken);
            if (followUp.DoctorId == doctor.Id)
            {
                return await MapAsync(followUp, cancellationToken);
            }
        }

        if (asReception && await clinicAccess.HasClinicStaffAccessAsync(userId, followUp.ClinicId, cancellationToken))
        {
            return await MapAsync(followUp, cancellationToken);
        }

        throw DomainException.NotFound("follow_up_not_found", "That follow-up was not found.");
    }

    private async Task<IReadOnlyList<FollowUpDetails>> MapManyAsync(
        IReadOnlyList<PatientFollowUp> rows,
        CancellationToken cancellationToken)
    {
        var result = new List<FollowUpDetails>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(await MapAsync(row, cancellationToken));
        }

        return result;
    }

    private async Task<FollowUpDetails> MapAsync(PatientFollowUp followUp, CancellationToken cancellationToken)
    {
        var reminders = await store.ListRemindersAsync(followUp.Id, cancellationToken);
        var today = TodayLocal(followUp.TimeZoneId);
        return new FollowUpDetails(
            followUp.Id,
            followUp.PatientUserId,
            followUp.FamilyMemberId,
            followUp.PatientName,
            followUp.DoctorId,
            await DoctorNameAsync(followUp.DoctorId, cancellationToken),
            followUp.ClinicId,
            await ClinicNameAsync(followUp.ClinicId, cancellationToken),
            followUp.BranchId,
            followUp.SourceAppointmentId,
            followUp.SourceConsultationId,
            followUp.RecommendedDate,
            followUp.FollowUpNote,
            followUp.RequiresBooking,
            followUp.Status.ToString(),
            followUp.ResolveDisplayStatus(today),
            followUp.LinkedAppointmentId,
            followUp.LinkedAppointmentId is not null,
            followUp.CreatedAtUtc,
            followUp.UpdatedAtUtc,
            followUp.CancelledAtUtc,
            followUp.CompletedAtUtc,
            followUp.TimeZoneId,
            reminders.Select(item => new FollowUpReminderDetails(
                item.Id,
                item.ReminderType.ToString(),
                item.ScheduledAtUtc,
                item.DeliveryStatus.ToString(),
                item.SentAtUtc,
                item.IdempotencyKey)).ToArray());
    }

    private async Task<Doctor> RequireDoctorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken)
            ?? throw DomainException.Forbidden("doctor_required", "Only an authorized doctor can manage follow-ups.");
        if (doctor.Status != VerificationStatus.Approved)
        {
            throw DomainException.Forbidden("doctor_not_approved", "Complete doctor verification before managing follow-ups.");
        }

        return doctor;
    }

    public Task<FollowUpDetails> GetForReceptionAsync(Guid userId, Guid followUpId, CancellationToken cancellationToken) =>
        GetAuthorizedAsync(userId, followUpId, asDoctor: false, asPatient: false, asReception: true, cancellationToken);

    private async Task<string> DoctorNameAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await store.FindDoctorDisplayNameAsync(doctorId, cancellationToken) ?? "your doctor";

    private async Task<string> ClinicNameAsync(Guid clinicId, CancellationToken cancellationToken) =>
        await store.FindClinicNameAsync(clinicId, cancellationToken) ?? "Clinic";

    private DateOnly TodayLocal(string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Zone(timeZoneId)).DateTime);

    private static TimeZoneInfo Zone(string timeZoneId)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
        {
            return zone;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out zone)
            || TimeZoneInfo.TryFindSystemTimeZoneById("India Standard Time", out zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }

    private static AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, DateTimeOffset.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
