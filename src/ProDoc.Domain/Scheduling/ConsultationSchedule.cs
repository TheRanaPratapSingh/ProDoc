using ProDoc.Domain.Common;

namespace ProDoc.Domain.Scheduling;

public sealed class WeeklyHours
{
    private WeeklyHours()
    {
    }

    public Guid Id { get; private set; }

    public Guid ScheduleId { get; private set; }

    public DayOfWeek Day { get; private set; }

    public TimeOnly StartsAt { get; private set; }

    public TimeOnly EndsAt { get; private set; }

    internal static WeeklyHours Create(Guid scheduleId, DayOfWeek day, TimeOnly startsAt, TimeOnly endsAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Day = day,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
}

public sealed class ScheduleBreak
{
    private ScheduleBreak()
    {
    }

    public Guid Id { get; private set; }

    public Guid ScheduleId { get; private set; }

    public DayOfWeek Day { get; private set; }

    public TimeOnly StartsAt { get; private set; }

    public TimeOnly EndsAt { get; private set; }

    internal static ScheduleBreak Create(Guid scheduleId, DayOfWeek day, TimeOnly startsAt, TimeOnly endsAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Day = day,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
}

public sealed class ScheduleClosure
{
    private ScheduleClosure()
    {
    }

    public Guid Id { get; private set; }

    public Guid ScheduleId { get; private set; }

    public DateOnly Date { get; private set; }

    public string? Reason { get; private set; }

    internal static ScheduleClosure Create(Guid scheduleId, DateOnly date, string? reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Date = date,
            Reason = reason
        };
}

public sealed class ScheduleExtra
{
    private ScheduleExtra()
    {
    }

    public Guid Id { get; private set; }

    public Guid ScheduleId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartsAt { get; private set; }

    public TimeOnly EndsAt { get; private set; }

    internal static ScheduleExtra Create(Guid scheduleId, DateOnly date, TimeOnly startsAt, TimeOnly endsAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Date = date,
            StartsAt = startsAt,
            EndsAt = endsAt
        };
}

public sealed class ConsultationSchedule
{
    public const string CurrencyCode = "INR";

    public const string DefaultTimeZone = "Asia/Kolkata";

    private List<WeeklyHours> _hours = [];
    private List<ScheduleBreak> _breaks = [];
    private List<ScheduleClosure> _closures = [];
    private List<ScheduleExtra> _extras = [];

    private ConsultationSchedule()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public decimal ConsultationFee { get; private set; }

    public int SlotDurationMinutes { get; private set; }

    public int BufferMinutes { get; private set; }

    public int? MaxPatients { get; private set; }

    public DateOnly? EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public string TimeZoneId { get; private set; } = DefaultTimeZone;

    public bool IsPublished { get; private set; }

    public bool RequiresApproval { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public IReadOnlyCollection<WeeklyHours> Hours => _hours;

    public IReadOnlyCollection<ScheduleBreak> Breaks => _breaks;

    public IReadOnlyCollection<ScheduleClosure> Closures => _closures;

    public IReadOnlyCollection<ScheduleExtra> Extras => _extras;

    public static ConsultationSchedule Create(
        Guid doctorId,
        Guid? clinicId,
        Guid? branchId,
        decimal consultationFee,
        int slotDurationMinutes,
        DateTimeOffset now)
    {
        if (doctorId == Guid.Empty)
        {
            throw DomainException.Validation("schedule_invalid", "Choose a doctor.");
        }

        if ((clinicId is null) != (branchId is null))
        {
            throw DomainException.Validation("schedule_invalid", "Choose both a clinic and a branch, or neither for independent practice.");
        }

        var schedule = new ConsultationSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            IsPublished = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        schedule.SetFee(consultationFee, now);
        schedule.SetDuration(slotDurationMinutes, now);
        schedule.BufferMinutes = 0;
        schedule.TimeZoneId = DefaultTimeZone;
        return schedule;
    }

    public void SetFee(decimal consultationFee, DateTimeOffset now)
    {
        if (consultationFee < 0 || consultationFee > 100_000 || decimal.Round(consultationFee, 2) != consultationFee)
        {
            throw DomainException.Validation("fee_invalid", "Enter a consultation fee from 0 to 100000 INR with at most two decimal places.");
        }

        ConsultationFee = consultationFee;
        Touch(now);
    }

    public void SetDuration(int slotDurationMinutes, DateTimeOffset now)
    {
        if (slotDurationMinutes is < 5 or > 120 || slotDurationMinutes % 5 != 0)
        {
            throw DomainException.Validation("duration_invalid", "Choose a slot length from 5 to 120 minutes in steps of 5.");
        }

        SlotDurationMinutes = slotDurationMinutes;
        Touch(now);
    }

    public void SetBuffer(int bufferMinutes, DateTimeOffset now)
    {
        if (bufferMinutes is < 0 or > 60 || bufferMinutes % 5 != 0)
        {
            throw DomainException.Validation("buffer_invalid", "Choose a buffer from 0 to 60 minutes in steps of 5.");
        }

        BufferMinutes = bufferMinutes;
        Touch(now);
    }

    public void SetCapacity(int? maxPatients, DateTimeOffset now)
    {
        if (maxPatients is < 1 or > 200)
        {
            throw DomainException.Validation("capacity_invalid", "Choose a session capacity from 1 to 200, or leave it open.");
        }

        MaxPatients = maxPatients;
        Touch(now);
    }

    public void SetEffectivePeriod(DateOnly? from, DateOnly? to, DateTimeOffset now)
    {
        if (from is DateOnly start && to is DateOnly end && end < start)
        {
            throw DomainException.Validation("schedule_window_invalid", "The schedule end date must be on or after the start date.");
        }

        EffectiveFrom = from;
        EffectiveTo = to;
        Touch(now);
    }

    public void SetTimeZone(string? timeZoneId, DateTimeOffset now)
    {
        var value = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZone : timeZoneId.Trim();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(value, out _))
        {
            throw DomainException.Validation("timezone_invalid", "Choose a recognised clinic time zone.");
        }

        TimeZoneId = value;
        Touch(now);
    }

    public ScheduleExtra AddExtra(DateOnly date, TimeOnly startsAt, TimeOnly endsAt, DateTimeOffset now)
    {
        if (endsAt <= startsAt)
        {
            throw DomainException.Validation("hours_invalid", "A working period must end after it starts.");
        }

        if (_extras.Any(item => item.Date == date && item.StartsAt < endsAt && startsAt < item.EndsAt))
        {
            throw DomainException.Validation("hours_invalid", "One-time hours on the same day cannot overlap.");
        }

        var extra = ScheduleExtra.Create(Id, date, startsAt, endsAt);
        _extras.Add(extra);
        Touch(now);
        return extra;
    }

    public void RemoveExtra(Guid extraId, DateTimeOffset now)
    {
        var extra = _extras.SingleOrDefault(item => item.Id == extraId)
            ?? throw DomainException.NotFound("extra_not_found", "That one-time session was not found.");
        _extras.Remove(extra);
        Touch(now);
    }

    public void ReplaceHours(IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> windows, DateTimeOffset now)
    {
        var ordered = windows
            .Select(window => (window.Day, window.StartsAt, window.EndsAt))
            .OrderBy(window => window.Day)
            .ThenBy(window => window.StartsAt)
            .ToArray();
        foreach (var window in ordered)
        {
            if (window.EndsAt <= window.StartsAt)
            {
                throw DomainException.Validation("hours_invalid", "A working period must end after it starts.");
            }
        }

        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (previous.Day == current.Day && current.StartsAt < previous.EndsAt)
            {
                throw DomainException.Validation("hours_invalid", "Working periods on the same day cannot overlap.");
            }
        }

        _hours.Clear();
        foreach (var window in ordered)
        {
            _hours.Add(WeeklyHours.Create(Id, window.Day, window.StartsAt, window.EndsAt));
        }

        RemoveBreaksOutsideHours();
        Touch(now);
    }

    public void ReplaceBreaks(IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> breaks, DateTimeOffset now)
    {
        foreach (var item in breaks)
        {
            if (item.EndsAt <= item.StartsAt || !FitsInsideHours(item.Day, item.StartsAt, item.EndsAt))
            {
                throw DomainException.Validation("break_invalid", "A break must sit inside working hours.");
            }
        }

        _breaks.Clear();
        foreach (var item in breaks)
        {
            _breaks.Add(ScheduleBreak.Create(Id, item.Day, item.StartsAt, item.EndsAt));
        }

        Touch(now);
    }

    public ScheduleClosure AddClosure(DateOnly date, string? reason, DateTimeOffset now)
    {
        if (_closures.Any(item => item.Date == date))
        {
            throw DomainException.Conflict("closure_exists", "That date is already closed.");
        }

        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > 120 })
        {
            throw DomainException.Validation("closure_invalid", "Enter a closure reason of 120 characters or fewer.");
        }

        var closure = ScheduleClosure.Create(Id, date, trimmed);
        _closures.Add(closure);
        Touch(now);
        return closure;
    }

    public void RemoveClosure(Guid closureId, DateTimeOffset now)
    {
        var closure = _closures.SingleOrDefault(item => item.Id == closureId)
            ?? throw DomainException.NotFound("closure_not_found", "That closure was not found.");
        _closures.Remove(closure);
        Touch(now);
    }

    public void SetRequiresApproval(bool requiresApproval, DateTimeOffset now)
    {
        RequiresApproval = requiresApproval;
        Touch(now);
    }

    public void Publish(DateTimeOffset now)
    {
        if (_hours.Count == 0)
        {
            throw DomainException.Validation("schedule_incomplete", "Add working hours before publishing.");
        }

        IsPublished = true;
        Touch(now);
    }

    public void Unpublish(DateTimeOffset now)
    {
        IsPublished = false;
        Touch(now);
    }

    public IReadOnlyList<(TimeOnly Start, TimeOnly End)> GenerateSlots(DateOnly date)
    {
        if (EffectiveFrom is DateOnly from && date < from)
        {
            return [];
        }

        if (EffectiveTo is DateOnly to && date > to)
        {
            return [];
        }

        if (_closures.Any(item => item.Date == date))
        {
            return [];
        }

        var day = date.DayOfWeek;
        var windows = _hours
            .Where(item => item.Day == day)
            .Select(item => (item.StartsAt, item.EndsAt))
            .Concat(_extras.Where(item => item.Date == date).Select(item => (item.StartsAt, item.EndsAt)))
            .OrderBy(item => item.StartsAt)
            .ToArray();
        var slots = new List<(TimeOnly Start, TimeOnly End)>();
        var step = SlotDurationMinutes + BufferMinutes;
        foreach (var window in windows)
        {
            var cursor = window.StartsAt;
            while (cursor.AddMinutes(SlotDurationMinutes) <= window.EndsAt)
            {
                var end = cursor.AddMinutes(SlotDurationMinutes);
                var blocked = _breaks.Any(item => item.Day == day && item.StartsAt < end && cursor < item.EndsAt);
                if (!blocked)
                {
                    slots.Add((cursor, end));
                    if (MaxPatients is int capacity && slots.Count >= capacity)
                    {
                        return slots;
                    }
                }

                var next = cursor.AddMinutes(step);
                if (next <= cursor)
                {
                    break;
                }

                cursor = next;
            }
        }

        return slots;
    }

    public static IReadOnlyList<(TimeOnly Start, TimeOnly End)> ClipToOpenHours(
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> slots,
        IReadOnlyList<(TimeOnly OpensAt, TimeOnly ClosesAt)> openWindows) =>
        slots.Where(slot => openWindows.Any(window => window.OpensAt <= slot.Start && slot.End <= window.ClosesAt)).ToArray();

    private bool FitsInsideHours(DayOfWeek day, TimeOnly start, TimeOnly end) =>
        _hours.Any(window => window.Day == day && window.StartsAt <= start && end <= window.EndsAt);

    private void RemoveBreaksOutsideHours() =>
        _breaks.RemoveAll(item => !FitsInsideHours(item.Day, item.StartsAt, item.EndsAt));

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
