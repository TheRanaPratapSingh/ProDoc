using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public sealed class ScheduleService(IScheduleStore schedules, IProviderStore providers, ISlotOccupancy occupancy, IClock clock)
{
    public async Task<IReadOnlyList<ScheduleDetails>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorByUserIdAsync(userId, tracking: false, cancellationToken);
        var ownedClinics = await providers.ListClinicsByOwnerAsync(userId, cancellationToken);
        var items = new List<ConsultationSchedule>();
        if (doctor is not null)
        {
            items.AddRange(await schedules.ListForDoctorAsync(doctor.Id, cancellationToken));
        }

        var clinicIds = ownedClinics.Select(clinic => clinic.Id).ToArray();
        if (clinicIds.Length > 0)
        {
            var clinicSchedules = await schedules.ListForClinicsAsync(clinicIds, cancellationToken);
            items.AddRange(clinicSchedules.Where(item => items.All(existing => existing.Id != item.Id)));
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
        await schedules.AddAsync(schedule, cancellationToken);
        await schedules.SaveChangesAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        var now = clock.UtcNow;
        schedule.SetFee(consultationFee, now);
        schedule.SetDuration(slotDurationMinutes, now);
        schedule.SetBuffer(bufferMinutes, now);
        schedule.SetCapacity(maxPatients, now);
        schedule.SetEffectivePeriod(effectiveFrom, effectiveTo, now);
        schedule.SetTimeZone(timeZoneId, now);
        schedule.SetRequiresApproval(requiresApproval, now);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ReplaceHoursAsync(
        Guid userId,
        Guid scheduleId,
        IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> windows,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.ReplaceHours(windows, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> ReplaceBreaksAsync(
        Guid userId,
        Guid scheduleId,
        IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> breaks,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.ReplaceBreaks(breaks, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> AddClosureAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        string? reason,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.AddClosure(date, reason, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> RemoveClosureAsync(
        Guid userId,
        Guid scheduleId,
        Guid closureId,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.RemoveClosure(closureId, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> SetPublishedAsync(
        Guid userId,
        Guid scheduleId,
        bool published,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        if (published)
        {
            schedule.Publish(clock.UtcNow);
        }
        else
        {
            schedule.Unpublish(clock.UtcNow);
        }

        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> AddExtraAsync(
        Guid userId,
        Guid scheduleId,
        DateOnly date,
        TimeOnly startsAt,
        TimeOnly endsAt,
        CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.AddExtra(date, startsAt, endsAt, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task<ScheduleDetails> RemoveExtraAsync(Guid userId, Guid scheduleId, Guid extraId, CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedule.RemoveExtra(extraId, clock.UtcNow);
        await schedules.SaveChangesAsync(cancellationToken);
        return ToDetails(schedule);
    }

    public async Task DeleteAsync(Guid userId, Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await EditableAsync(userId, scheduleId, cancellationToken);
        schedules.Remove(schedule);
        await schedules.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DayAvailability>> GetCalendarAsync(
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var today = ClinicToday();
        if (to < from || to > from.AddDays(60) || from < today || to > today.AddDays(60))
        {
            throw DomainException.Validation("date_invalid", "Choose a range from today through the next 60 days.");
        }

        var published = await schedules.ListPublishedForDoctorAsync(doctorId, cancellationToken);
        var selected = branchId is Guid branch
            ? published.Where(item => item.BranchId == branch).ToArray()
            : published.ToArray();
        var days = new List<DayAvailability>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            DayAvailability? best = null;
            foreach (var schedule in selected)
            {
                var day = await DescribeDayAsync(schedule, date, cancellationToken);
                var candidate = new DayAvailability(date, day.Status, day.Slots.Count, day.Slots.Count == 0 ? null : day.Slots[0].Start, schedule.Id);
                if (best is null || candidate.SlotCount > best.SlotCount)
                {
                    best = candidate;
                }
            }

            days.Add(best ?? new DayAvailability(date, "unconfigured", 0, null, null));
        }

        return days;
    }

    public async Task<DayAvailability?> NextAvailableAsync(Guid doctorId, Guid? branchId, CancellationToken cancellationToken)
    {
        var today = ClinicToday();
        var days = await GetCalendarAsync(doctorId, today, today.AddDays(60), branchId, cancellationToken);
        return days.FirstOrDefault(day => day.Status == "available");
    }

    public async Task<SlotAvailability> GetAvailabilityAsync(Guid scheduleId, DateOnly date, CancellationToken cancellationToken)
    {
        var today = ClinicToday();
        if (date < today || date > today.AddDays(60))
        {
            throw DomainException.Validation("date_invalid", "Choose a date from today through the next 60 days.");
        }

        var schedule = await schedules.FindAsync(scheduleId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
        if (!schedule.IsPublished || !await IsPubliclyEligibleAsync(schedule, today, cancellationToken))
        {
            throw DomainException.NotFound("schedule_not_found", "That schedule is not available.");
        }

        var day = await DescribeDayAsync(schedule, date, cancellationToken);
        return new SlotAvailability(schedule.Id, date, day.Slots);
    }

    private async Task<(IReadOnlyList<AvailableSlot> Slots, string Status)> DescribeDayAsync(
        ConsultationSchedule schedule,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (schedule.Closures.Any(item => item.Date == date))
        {
            return ([], "holiday");
        }

        var clipped = await ClippedSlotsAsync(schedule, date, cancellationToken);
        if (clipped.Closed)
        {
            return ([], "holiday");
        }

        var future = FilterPast(schedule, date, clipped.Slots, new HashSet<TimeOnly>());
        var visible = await VisibleSlotsAsync(schedule, date, future, cancellationToken);
        if (visible.Count > 0)
        {
            return (visible, "available");
        }

        return future.Count > 0 ? ([], "full") : ([], "unavailable");
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
        CancellationToken cancellationToken)
    {
        if (schedule.BranchId is not Guid branchId)
        {
            return FilterPast(schedule, date, slots, new HashSet<TimeOnly>());
        }

        var occupied = await occupancy.OccupiedLocalStartsAsync(schedule.DoctorId, branchId, date, clock.UtcNow, cancellationToken);
        return FilterPast(schedule, date, slots, occupied);
    }

    private IReadOnlyList<AvailableSlot> FilterPast(
        ConsultationSchedule schedule,
        DateOnly date,
        IReadOnlyList<AvailableSlot> slots,
        IReadOnlySet<TimeOnly> occupied)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(schedule.TimeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;
        var localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, zone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        return slots.Where(slot => !occupied.Contains(slot.Start) && (date > localDate || slot.Start > localTime)).ToArray();
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

    private async Task<ConsultationSchedule> EditableAsync(Guid userId, Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await schedules.FindAsync(scheduleId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        var doctor = await providers.FindDoctorAsync(schedule.DoctorId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        if (doctor.UserId == userId)
        {
            return schedule;
        }

        if (schedule.ClinicId is Guid clinicId)
        {
            var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken);
            if (clinic?.OwnerUserId == userId)
            {
                return schedule;
            }
        }

        throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
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
            throw DomainException.NotFound("schedule_not_found", "That schedule was not found.");
        }

        var affiliation = await providers.FindAffiliationAsync(doctor.Id, selectedClinic, cancellationToken)
            ?? throw DomainException.Validation("affiliation_required", "Link the doctor to this clinic before adding its hours.");
        if (affiliation.BranchId is Guid linkedBranch && linkedBranch != selectedBranch)
        {
            throw DomainException.Validation("branch_invalid", "This doctor is linked to a different branch.");
        }
    }

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
            schedule.EffectiveFrom,
            schedule.EffectiveTo,
            schedule.TimeZoneId,
            schedule.IsPublished,
            schedule.RequiresApproval,
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
}
