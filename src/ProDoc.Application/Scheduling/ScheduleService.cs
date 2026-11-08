using ProDoc.Application.Abstractions;
using ProDoc.Application.Appointments;
using ProDoc.Application.Notifications;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public sealed class ScheduleService(
    IScheduleStore schedules,
    IProviderStore providers,
    IAppointmentStore appointments,
    IQueueStore queue,
    IClinicAccess clinicAccess,
    ISlotOccupancy occupancy,
    NotificationService notifications,
    IClock clock)
{
    public async Task<IReadOnlyList<ScheduleDetails>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = new List<ConsultationSchedule>();
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        if (doctor is not null)
        {
            items.AddRange(await schedules.ListForDoctorAsync(doctor.Id, cancellationToken));
        }

        var ownedClinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        var clinicIds = ownedClinics.Select(clinic => clinic.Id).ToArray();
        if (clinicIds.Length > 0)
        {
            var clinicSchedules = await schedules.ListForClinicsAsync(clinicIds, cancellationToken);
            items.AddRange(clinicSchedules.Where(item => items.All(existing => existing.Id != item.Id)));
        }

        var staffMemberships = await queue.ListStaffForUserAsync(userId, cancellationToken);
        foreach (var membership in staffMemberships)
        {
            var staffSchedules = await schedules.ListForClinicsAsync([membership.ClinicId], cancellationToken);
            var assigned = await clinicAccess.ListAssignedDoctorIdsAsync(userId, membership.ClinicId, cancellationToken);
            var allowed = assigned.Count > 0 ? assigned.ToHashSet() : null;
            foreach (var schedule in staffSchedules)
            {
                if (items.Any(existing => existing.Id == schedule.Id))
                {
                    continue;
                }

                if (allowed is not null && !allowed.Contains(schedule.DoctorId))
                {
                    continue;
                }

                if (schedule.BranchId is Guid branchId && !membership.CoversBranch(branchId))
                {
                    continue;
                }

                items.Add(schedule);
            }
        }

        var seededIds = new HashSet<Guid>();
        foreach (var schedule in items.Where(item => item.Hours.Count == 0))
        {
            var tracked = await schedules.FindAsync(schedule.Id, tracking: true, cancellationToken);
            if (tracked is null || tracked.Hours.Count > 0)
            {
                continue;
            }

            tracked.ApplyDefaultWeekdayHours(clock.UtcNow);
            if (!tracked.IsPublished)
            {
                tracked.Publish(clock.UtcNow);
            }

            seededIds.Add(tracked.Id);
        }

        if (seededIds.Count > 0)
        {
            await schedules.SaveChangesAsync(null, cancellationToken);
            for (var index = 0; index < items.Count; index++)
            {
                if (!seededIds.Contains(items[index].Id))
                {
                    continue;
                }

                var refreshed = await schedules.FindAsync(items[index].Id, tracking: false, cancellationToken);
                if (refreshed is not null)
                {
                    items[index] = refreshed;
                }
            }
        }

        return items.Select(ToDetails).ToArray();
    }

    public async Task<ScheduleDetails> CreateAsync(
        Guid userId,
        Guid? doctorId,
        Guid? clinicId,
        Guid? branchId,
        decimal consultationFee,
        int slotDurationMinutes,
        int bufferMinutes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await ResolveDoctorAsync(userId, doctorId, cancellationToken);
        await AuthorizePlacementAsync(userId, doctor, clinicId, branchId, cancellationToken);
        if (await schedules.ExistsAsync(doctor.Id, clinicId, branchId, cancellationToken))
        {
            throw DomainException.Conflict("schedule_exists", "A schedule already exists for this place.");
        }

        var schedule = ConsultationSchedule.Create(doctor.Id, clinicId, branchId, consultationFee, slotDurationMinutes, clock.UtcNow);
        schedule.SetBuffer(bufferMinutes, clock.UtcNow);
        schedule.ApplyDefaultWeekdayHours(clock.UtcNow);
        schedule.Publish(clock.UtcNow);
        await schedules.AddAsync(schedule, cancellationToken);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleCreated, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> UpdateTermsAsync(
        Guid userId,
        Guid scheduleId,
        decimal consultationFee,
        int slotDurationMinutes,
        int bufferMinutes,
        int? maxPatients,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        string? timeZoneId,
        bool requiresApproval,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken,
        string? bookingMode = null,
        bool confirmBookingModeChange = false)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        var now = clock.UtcNow;
        var previousMode = schedule.BookingMode;
        var nextMode = ParseBookingMode(bookingMode, previousMode);
        if (nextMode != previousMode)
        {
            var futureCount = await CountFutureActiveBookingsAsync(schedule, now, cancellationToken);
            if (futureCount > 0 && !confirmBookingModeChange)
            {
                throw DomainException.Conflict(
                    "booking_mode_change_requires_confirmation",
                    $"{futureCount} future booking(s) use the current mode and will keep their original times/tokens. Confirm to switch mode for new bookings only.");
            }
        }

        schedule.SetFee(consultationFee, now);
        schedule.SetDuration(slotDurationMinutes, now);
        schedule.SetBuffer(bufferMinutes, now);
        schedule.SetCapacity(maxPatients, now);
        schedule.SetEffectivePeriod(effectiveFrom, effectiveTo, now);
        schedule.SetTimeZone(timeZoneId, now);
        schedule.SetRequiresApproval(requiresApproval, now);
        if (nextMode != previousMode)
        {
            schedule.SetBookingMode(nextMode, now);
            await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleBookingModeChanged, metadata), cancellationToken);
        }
        else
        {
            await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleUpdated, metadata), cancellationToken);
        }

        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ReplaceHoursAsync(
        Guid userId,
        Guid scheduleId,
        IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> windows,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.ReplaceHours(windows, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleHoursReplaced, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ReplaceBreaksAsync(
        Guid userId,
        Guid scheduleId,
        IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> breaks,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.ReplaceBreaks(breaks, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleBreaksReplaced, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> AddClosureAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        string? reason,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.AddClosure(date, reason, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleClosureAdded, metadata), cancellationToken);
        await NotifyLeaveAsync(schedule, date, reason, cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> RemoveClosureAsync(
        Guid userId,
        Guid scheduleId,
        Guid closureId,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.RemoveClosure(closureId, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleClosureRemoved, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> SetPublishedAsync(
        Guid userId,
        Guid scheduleId,
        bool published,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        if (published)
        {
            schedule.Publish(clock.UtcNow);
        }
        else
        {
            schedule.Unpublish(clock.UtcNow);
        }

        await schedules.SaveChangesAsync(Audit(userId, AuditActions.SchedulePublicationChanged, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> AddExtraAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly startsAt,
        TimeOnly endsAt,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.AddExtra(date, startsAt, endsAt, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleExtraAdded, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> RemoveExtraAsync(
        Guid userId,
        Guid scheduleId,
        Guid extraId,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedule.RemoveExtra(extraId, clock.UtcNow);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleExtraRemoved, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ReplaceDayOverrideAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        IReadOnlyCollection<(TimeOnly StartsAt, TimeOnly EndsAt)> windows,
        bool available,
        string? unavailableReason,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        var now = clock.UtcNow;
        var existingClosure = schedule.Closures.FirstOrDefault(item => item.Date == date);
        if (available)
        {
            if (existingClosure is not null)
            {
                schedule.RemoveClosure(existingClosure.Id, now);
            }

            schedule.ReplaceExtrasForDate(date, windows, now);
            if (!schedule.IsPublished)
            {
                schedule.Publish(now);
            }
        }
        else
        {
            schedule.ReplaceExtrasForDate(date, [], now);
            if (existingClosure is null)
            {
                schedule.AddClosure(date, unavailableReason ?? "Unavailable", now);
            }
        }

        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleDayOverrideReplaced, metadata), cancellationToken);
        if (!available)
        {
            await NotifyLeaveAsync(schedule, date, unavailableReason, cancellationToken);
        }

        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ResetDayOverrideAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        var now = clock.UtcNow;
        schedule.ReplaceExtrasForDate(date, [], now);
        var existingClosure = schedule.Closures.FirstOrDefault(item => item.Date == date);
        if (existingClosure is not null)
        {
            schedule.RemoveClosure(existingClosure.Id, now);
        }

        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleDayOverrideReplaced, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> CopyDayScheduleAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly sourceDate,
        IReadOnlyCollection<DateOnly> targetDates,
        bool overwriteExisting,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        var now = clock.UtcNow;
        var sourceClosed = schedule.Closures.Any(item => item.Date == sourceDate);
        var sourceReason = schedule.Closures.FirstOrDefault(item => item.Date == sourceDate)?.Reason;
        var sourceExtras = schedule.Extras.Where(item => item.Date == sourceDate).OrderBy(item => item.StartsAt).ToArray();
        var sourceWindows = sourceExtras.Length > 0
            ? sourceExtras.Select(item => (item.StartsAt, item.EndsAt)).ToArray()
            : schedule.Hours
                .Where(item => item.Day == sourceDate.DayOfWeek)
                .Select(item => (item.StartsAt, item.EndsAt))
                .OrderBy(item => item.StartsAt)
                .ToArray();

        foreach (var target in targetDates.Distinct().Where(date => date != sourceDate).OrderBy(date => date))
        {
            var hasOverride = schedule.Extras.Any(item => item.Date == target) || schedule.Closures.Any(item => item.Date == target);
            if (hasOverride && !overwriteExisting)
            {
                continue;
            }

            var existingClosure = schedule.Closures.FirstOrDefault(item => item.Date == target);
            if (existingClosure is not null)
            {
                schedule.RemoveClosure(existingClosure.Id, now);
            }

            if (sourceClosed)
            {
                schedule.ReplaceExtrasForDate(target, [], now);
                schedule.AddClosure(target, sourceReason ?? "Copied leave", now);
            }
            else
            {
                schedule.ReplaceExtrasForDate(target, sourceWindows, now);
            }
        }

        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleDayCopied, metadata), cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ClosureImpact> PreviewClosureImpactAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion: null, cancellationToken, requireVersion: false);
        if (schedule.BranchId is not Guid branchId)
        {
            return new ClosureImpact(0);
        }

        var occupied = await occupancy.OccupiedLocalStartsAsync(
            schedule.DoctorId,
            branchId,
            date,
            clock.UtcNow,
            cancellationToken);
        return new ClosureImpact(occupied.Count);
    }

    public async Task<ScheduleDetails> EnsureDefaultHoursAsync(
        Guid userId,
        Guid scheduleId,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        if (schedule.Hours.Count == 0)
        {
            schedule.ApplyDefaultWeekdayHours(clock.UtcNow);
            if (!schedule.IsPublished)
            {
                schedule.Publish(clock.UtcNow);
            }

            await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleHoursReplaced, metadata), cancellationToken);
        }

        return ToDetails(schedule);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid scheduleId,
        string? expectedRowVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, expectedRowVersion, cancellationToken);
        schedules.Remove(schedule);
        await schedules.SaveChangesAsync(Audit(userId, AuditActions.ScheduleDeleted, metadata), cancellationToken);
    }

    public async Task<AppResult<IReadOnlyList<DayAvailability>>> GetCalendarAsync(
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        CancellationToken cancellationToken,
        AvailabilityHorizon horizon = AvailabilityHorizon.PatientTwoWeeks)
    {
        var today = ClinicToday();
        if (DateValidation.ValidateInclusiveRange(from, to, maxInclusiveDays: 62) is { } rangeError)
        {
            return AppResult<IReadOnlyList<DayAvailability>>.Failure(rangeError);
        }

        var (windowStart, windowEnd) = ResolveHorizonWindow(today, horizon);
        if (to < windowStart || from > windowEnd)
        {
            return AppResult<IReadOnlyList<DayAvailability>>.Validation(
                "date_invalid",
                horizon == AvailabilityHorizon.PatientTwoWeeks
                    ? "Patient booking is limited to this week and next week."
                    : "Choose a range from today through the next 60 days.");
        }

        // Never expose dates outside the permitted horizon.
        var rangeStart = from < windowStart ? windowStart : from;
        var rangeEnd = to > windowEnd ? windowEnd : to;
        if (horizon == AvailabilityHorizon.StaffExtended)
        {
            // Staff month grids may start before today; keep past days as unavailable placeholders.
            rangeStart = from < windowStart ? windowStart : from;
        }

        var published = await schedules.ListPublishedForDoctorAsync(doctorId, cancellationToken);
        var selected = branchId is Guid branch
            ? published.Where(item => item.BranchId == branch).ToArray()
            : published.ToArray();
        var days = new List<DayAvailability>();
        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            // Past calendar days inside the patient 14-day window stay visible but not bookable.
            if (date < today)
            {
                days.Add(new DayAvailability(date, "unavailable", 0, null, null));
                continue;
            }

            if (date > windowEnd)
            {
                days.Add(new DayAvailability(date, "unavailable", 0, null, null));
                continue;
            }

            DayAvailability? best = null;
            foreach (var schedule in selected)
            {
                var day = await DescribeDayAsync(schedule, date, cancellationToken);
                var openSlots = day.Slots.Where(slot => slot.Available).ToArray();
                var candidate = new DayAvailability(
                    date,
                    day.Status,
                    openSlots.Length,
                    openSlots.Length == 0 ? null : openSlots[0].Start,
                    schedule.Id);
                if (best is null || candidate.SlotCount > best.SlotCount)
                {
                    best = candidate;
                }
            }

            days.Add(best ?? new DayAvailability(date, "unconfigured", 0, null, null));
        }

        return AppResult<IReadOnlyList<DayAvailability>>.Success(days);
    }

    public async Task<DayAvailability?> NextAvailableAsync(
        Guid doctorId,
        Guid? branchId,
        CancellationToken cancellationToken,
        AvailabilityHorizon horizon = AvailabilityHorizon.PatientTwoWeeks)
    {
        var today = ClinicToday();
        var (_, windowEnd) = ResolveHorizonWindow(today, horizon);
        var daysResult = await GetCalendarAsync(doctorId, today, windowEnd, branchId, cancellationToken, horizon);
        if (daysResult.IsFailure)
        {
            return null;
        }

        return daysResult.Value!.FirstOrDefault(day => day.Status is "available" or "partial");
    }

    public async Task<AppResult<SlotAvailability>> GetAvailabilityAsync(
        Guid scheduleId,
        DateOnly date,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null,
        AvailabilityHorizon horizon = AvailabilityHorizon.PatientTwoWeeks)
    {
        var today = ClinicToday();
        var (windowStart, windowEnd) = ResolveHorizonWindow(today, horizon);
        if (date < today || date < windowStart || date > windowEnd)
        {
            return AppResult<SlotAvailability>.Validation(
                "date_invalid",
                horizon == AvailabilityHorizon.PatientTwoWeeks
                    ? "Choose a date within this week or next week."
                    : "Choose a date from today through the next 60 days.");
        }

        var schedule = await schedules.FindAsync(scheduleId, tracking: false, cancellationToken);
        if (schedule is null || !schedule.IsPublished)
        {
            // Published is enough for patients rescheduling an existing visit; public listing is discovery-only.
            return AppResult<SlotAvailability>.NotFound("schedule_not_found", "That schedule is not available.");
        }

        var day = await DescribeDayAsync(schedule, date, cancellationToken, exceptAppointmentId);
        return AppResult<SlotAvailability>.Success(
            new SlotAvailability(schedule.Id, date, ToBookingModeName(schedule.BookingMode), day.Slots));
    }

    public static (DateOnly Start, DateOnly End) ResolveHorizonWindow(DateOnly today, AvailabilityHorizon horizon)
    {
        if (horizon == AvailabilityHorizon.StaffExtended)
        {
            return (today, today.AddDays(60));
        }

        // Monday of the clinic week containing today → Sunday of the following week (14 days).
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-daysFromMonday);
        return (weekStart, weekStart.AddDays(13));
    }

    private async Task<(IReadOnlyList<AvailableSlot> Slots, string Status)> DescribeDayAsync(
        ConsultationSchedule schedule,
        DateOnly date,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null)
    {
        if (schedule.Closures.Any(item => item.Date == date))
        {
            return ([], "leave");
        }

        var clipped = await ClippedSlotsAsync(schedule, date, cancellationToken);
        if (clipped.Closed)
        {
            return ([], "holiday");
        }

        if (clipped.Slots.Count == 0)
        {
            // No weekly hours / date override windows for this weekday.
            return ([], "unavailable");
        }

        var visible = await VisibleSlotsAsync(schedule, date, clipped.Slots, cancellationToken, exceptAppointmentId);
        var openCount = visible.Count(slot => slot.Available);
        if (openCount > 0)
        {
            // Some open + some booked (or past) slots → partially available.
            return (visible, openCount < visible.Count ? "partial" : "available");
        }

        return visible.Count > 0 ? (visible, "full") : ([], "unavailable");
    }

    private async Task<(IReadOnlyList<AvailableSlot> Slots, bool Closed)> ClippedSlotsAsync(
        ConsultationSchedule schedule,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var generated = schedule.GenerateSlots(date).Select(slot => new AvailableSlot(slot.Start, slot.End)).ToArray();
        if (schedule.BranchId is not Guid branchId)
        {
            return (generated, false);
        }

        var hours = await schedules.ListOperatingHoursAsync(branchId, cancellationToken);
        if (hours.Count == 0)
        {
            return (generated, false);
        }

        var closures = await schedules.ListBranchClosuresAsync(branchId, cancellationToken);
        if (closures.Any(item => item.Date == date) || hours.Any(item => item.Day == date.DayOfWeek && item.IsClosed))
        {
            return ([], true);
        }

        var windows = hours.Where(item => item.Day == date.DayOfWeek && !item.IsClosed).Select(item => (item.OpensAt, item.ClosesAt)).ToArray();
        if (windows.Length == 0)
        {
            return ([], true);
        }

        return (ConsultationSchedule.ClipToOpenHours(generated.Select(slot => (slot.Start, slot.End)).ToArray(), windows)
            .Select(slot => new AvailableSlot(slot.Start, slot.End)).ToArray(), false);
    }

    private async Task<IReadOnlyList<AvailableSlot>> VisibleSlotsAsync(
        ConsultationSchedule schedule,
        DateOnly date,
        IReadOnlyList<AvailableSlot> slots,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null)
    {
        IReadOnlyDictionary<TimeOnly, int> counts = new Dictionary<TimeOnly, int>();
        if (schedule.BranchId is Guid branchId)
        {
            counts = await occupancy.OccupancyCountsAsync(
                schedule.DoctorId,
                branchId,
                date,
                clock.UtcNow,
                cancellationToken,
                exceptAppointmentId);
        }

        return FilterPast(schedule, date, slots, counts);
    }

    private IReadOnlyList<AvailableSlot> FilterPast(
        ConsultationSchedule schedule,
        DateOnly date,
        IReadOnlyList<AvailableSlot> slots,
        IReadOnlyDictionary<TimeOnly, int> occupancyCounts)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(schedule.TimeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;
        var localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, zone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        var hourly = schedule.BookingMode == ScheduleBookingMode.HourlyQueue;
        var capacity = schedule.MaxPatients;
        // Prefixes are assigned from the full generated day order (before past filtering).
        var prefixByStart = hourly
            ? slots
                .Select((slot, index) => (slot.Start, Prefix: ConsultationSchedule.HourlyTokenPrefix(index)))
                .ToDictionary(item => item.Start, item => item.Prefix)
            : null;
        return slots
            .Where(slot => date > localDate || slot.Start > localTime)
            .Select(slot =>
            {
                var booked = occupancyCounts.TryGetValue(slot.Start, out var count) ? count : 0;
                if (hourly)
                {
                    int? remaining = capacity is int max ? Math.Max(0, max - booked) : null;
                    var available = capacity is null || booked < capacity;
                    prefixByStart!.TryGetValue(slot.Start, out var prefix);
                    return new AvailableSlot(
                        slot.Start,
                        slot.End,
                        Available: available,
                        BookedCount: booked,
                        Capacity: capacity,
                        Remaining: remaining,
                        TokenPrefix: prefix);
                }

                return new AvailableSlot(slot.Start, slot.End, Available: booked == 0);
            })
            .ToArray();
    }

    private async Task NotifyLeaveAsync(
        ConsultationSchedule schedule,
        DateOnly date,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var doctor = await providers.FindDoctorAsync(schedule.DoctorId, tracking: false, cancellationToken);
            var doctorName = !string.IsNullOrWhiteSpace(doctor?.DisplayName) ? doctor!.DisplayName! : "Doctor";
            var title = "Appointment Schedule Update";
            var body =
                $"{doctorName} has updated their availability for {date:d MMM yyyy}. Your appointment may be affected. Please review your appointment details.";

            var affected = await appointments.ListForDoctorAsync(schedule.DoctorId, date, date, cancellationToken);
            var patientIds = affected
                .Where(item => item.Status is AppointmentStatus.Confirmed or AppointmentStatus.Requested or AppointmentStatus.CheckedIn)
                .Where(item => item.AccountUserId != Guid.Empty)
                .Select(item => item.AccountUserId)
                .Distinct()
                .ToArray();

            if (patientIds.Length > 0)
            {
                await notifications.NotifyUsersAsync(
                    patientIds,
                    NotificationKinds.AppointmentScheduleUpdate,
                    schedule.Id,
                    title,
                    body,
                    essential: true,
                    cancellationToken);
            }

            if (schedule.ClinicId is not Guid clinicId)
            {
                return;
            }

            var staffTitle = "Doctor leave update";
            var staffBody = $"Doctor availability changed for {date:yyyy-MM-dd}" +
                            (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim()}");
            var staff = await queue.ListStaffAsync(clinicId, cancellationToken);
            var staffIds = staff
                .Where(member => member.IsActive && member.Role is RoleNames.Receptionist or RoleNames.ClinicStaff)
                .Select(member => member.UserId)
                .ToList();
            if (doctor?.UserId is Guid doctorUserId)
            {
                staffIds.Add(doctorUserId);
            }

            await notifications.NotifyUsersAsync(
                staffIds,
                NotificationKinds.AppointmentScheduleUpdate,
                schedule.Id,
                staffTitle,
                staffBody,
                essential: true,
                cancellationToken);
        }
        catch
        {
            // Leave persistence must not fail if notifications are unavailable.
        }
    }

    private DateOnly ClinicToday()
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(ConsultationSchedule.DefaultTimeZone, out var resolved)
            ? resolved
            : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, zone).DateTime);
    }

    private async Task<bool> IsPubliclyEligibleAsync(ConsultationSchedule schedule, DateOnly today, CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(schedule.DoctorId, tracking: false, cancellationToken);
        if (doctor is null || !doctor.IsPubliclyBookable(today))
        {
            return false;
        }

        if (schedule.ClinicId is not Guid clinicId || schedule.BranchId is not Guid branchId)
        {
            return schedule.ClinicId is null;
        }

        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken);
        return clinic is not null
            && clinic.IsPubliclyListed(today)
            && clinic.Branches.Any(branch => branch.Id == branchId && branch.IsActive);
    }

    private async Task<ConsultationSchedule> EditableAsync(
        Guid userId,
        Guid scheduleId,
        string? expectedRowVersion,
        CancellationToken cancellationToken,
        bool requireVersion = true)
    {
        var schedule = await schedules.FindAsync(scheduleId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        var doctor = await providers.FindDoctorAsync(schedule.DoctorId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        if (doctor.UserId != userId)
        {
            if (schedule.ClinicId is not Guid clinicId || schedule.BranchId is not Guid branchId)
            {
                throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
            }

            try
            {
                await clinicAccess.EnsureDoctorCoverageAsync(userId, clinicId, branchId, schedule.DoctorId, cancellationToken);
            }
            catch (DomainException)
            {
                throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
            }
        }

        if (requireVersion)
        {
            ApplyExpectedVersion(schedule, expectedRowVersion);
        }

        return schedule;
    }

    private void ApplyExpectedVersion(ConsultationSchedule schedule, string? expectedRowVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedRowVersion))
        {
            throw DomainException.Validation("row_version_required", "Reload the schedule and try again.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(expectedRowVersion.Trim());
        }
        catch (FormatException)
        {
            throw DomainException.Validation("row_version_invalid", "Reload the schedule and try again.");
        }

        schedules.ApplyExpectedRowVersion(schedule, bytes);
    }

    private async Task<Doctor> ResolveDoctorAsync(Guid userId, Guid? doctorId, CancellationToken cancellationToken)
    {
        if (doctorId is Guid selected)
        {
            return await providers.FindDoctorAsync(selected, tracking: false, cancellationToken)
                ?? throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
        }

        return await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken)
            ?? throw DomainException.Validation("doctor_not_found", "Start a doctor profile before adding a schedule.");
    }

    private async Task AuthorizePlacementAsync(
        Guid userId,
        Doctor doctor,
        Guid? clinicId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (clinicId is not Guid selectedClinic)
        {
            if (doctor.UserId != userId)
            {
                throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
            }

            return;
        }

        var clinic = await providers.FindClinicAsync(selectedClinic, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (branchId is not Guid selectedBranch || clinic.Branches.All(branch => branch.Id != selectedBranch || !branch.IsActive))
        {
            throw DomainException.Validation("branch_invalid", "Choose an active branch of that clinic.");
        }

        var doctorOwns = doctor.UserId == userId;
        var clinicOwns = clinic.OwnerUserId == userId;
        if (!doctorOwns && !clinicOwns)
        {
            try
            {
                await clinicAccess.EnsureDoctorCoverageAsync(userId, selectedClinic, selectedBranch, doctor.Id, cancellationToken);
            }
            catch (DomainException)
            {
                throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
            }
        }

        var affiliation = await providers.FindAffiliationAsync(doctor.Id, selectedClinic, cancellationToken)
            ?? throw DomainException.Validation("affiliation_required", "Link the doctor to this clinic before adding its hours.");
        if (affiliation.BranchId is Guid linkedBranch && linkedBranch != selectedBranch)
        {
            throw DomainException.Validation("branch_invalid", "This doctor is linked to a different branch.");
        }
    }

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);

    private async Task<int> CountFutureActiveBookingsAsync(
        ConsultationSchedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (schedule.BranchId is not Guid branchId)
        {
            return 0;
        }

        // Count confirmed/requested/held visits still ahead under this schedule.
        var from = ClinicToday();
        var to = from.AddDays(60);
        var days = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var counts = await occupancy.OccupancyCountsAsync(
                schedule.DoctorId,
                branchId,
                date,
                now,
                cancellationToken);
            days += counts.Values.Sum();
        }

        return days;
    }

    private static ScheduleBookingMode ParseBookingMode(string? bookingMode, ScheduleBookingMode fallback)
    {
        if (string.IsNullOrWhiteSpace(bookingMode))
        {
            return fallback;
        }

        return bookingMode.Trim().ToLowerInvariant() switch
        {
            "timedslots" or "timed" or "slots" or "fifteenminute" or "15minute" => ScheduleBookingMode.TimedSlots,
            "hourlyqueue" or "hourly" or "queue" => ScheduleBookingMode.HourlyQueue,
            _ => throw DomainException.Validation(
                "booking_mode_invalid",
                "Choose TimedSlots or HourlyQueue.")
        };
    }

    private static string ToBookingModeName(ScheduleBookingMode mode) =>
        mode == ScheduleBookingMode.HourlyQueue ? "HourlyQueue" : "TimedSlots";

    private static ScheduleDetails ToDetails(ConsultationSchedule schedule) =>
        new(
            schedule.Id,
            schedule.DoctorId,
            schedule.ClinicId,
            schedule.BranchId,
            schedule.ConsultationFee,
            ConsultationSchedule.CurrencyCode,
            schedule.SlotDurationMinutes,
            schedule.BufferMinutes,
            schedule.MaxPatients,
            ToBookingModeName(schedule.BookingMode),
            schedule.EffectiveFrom,
            schedule.EffectiveTo,
            schedule.TimeZoneId,
            schedule.IsPublished,
            schedule.RequiresApproval,
            EncodeRowVersion(schedule.RowVersion),
            schedule.Hours
                .OrderBy(item => item.Day)
                .ThenBy(item => item.StartsAt)
                .Select(item => new HoursModel(item.Id, item.Day, item.StartsAt, item.EndsAt))
                .ToArray(),
            schedule.Breaks
                .OrderBy(item => item.Day)
                .ThenBy(item => item.StartsAt)
                .Select(item => new BreakModel(item.Id, item.Day, item.StartsAt, item.EndsAt))
                .ToArray(),
            schedule.Closures
                .OrderBy(item => item.Date)
                .Select(item => new ClosureModel(item.Id, item.Date, item.Reason))
                .ToArray(),
            schedule.Extras
                .OrderBy(item => item.Date)
                .ThenBy(item => item.StartsAt)
                .Select(item => new ExtraModel(item.Id, item.Date, item.StartsAt, item.EndsAt))
                .ToArray());

    private static string EncodeRowVersion(byte[]? rowVersion) =>
        rowVersion is { Length: > 0 } ? Convert.ToBase64String(rowVersion) : string.Empty;
}
