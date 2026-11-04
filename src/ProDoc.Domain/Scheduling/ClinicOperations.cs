using ProDoc.Domain.Common;

namespace ProDoc.Domain.Scheduling;

public sealed class BranchOperatingHour
{
    private BranchOperatingHour()
    {
    }

    public Guid Id { get; private set; }

    public Guid BranchId { get; private set; }

    public DayOfWeek Day { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    public bool IsClosed { get; private set; }

    public static BranchOperatingHour Create(Guid branchId, DayOfWeek day, TimeOnly opensAt, TimeOnly closesAt, bool isClosed)
    {
        if (!isClosed && closesAt <= opensAt)
        {
            throw DomainException.Validation("hours_invalid", "A branch must close after it opens.");
        }

        return new BranchOperatingHour
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Day = day,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            IsClosed = isClosed
        };
    }
}

public sealed class BranchClosure
{
    private BranchClosure()
    {
    }

    public Guid Id { get; private set; }

    public Guid BranchId { get; private set; }

    public DateOnly Date { get; private set; }

    public string? Reason { get; private set; }

    public static BranchClosure Create(Guid branchId, DateOnly date, string? reason)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > 120 })
        {
            throw DomainException.Validation("closure_invalid", "Enter a closure reason of 120 characters or fewer.");
        }

        return new BranchClosure
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Date = date,
            Reason = trimmed
        };
    }
}

public sealed class ClinicFacility
{
    private ClinicFacility()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public static ClinicFacility Create(Guid clinicId, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is < 2 or > 80)
        {
            throw DomainException.Validation("facility_invalid", "Enter a facility name from 2 to 80 characters.");
        }

        return new ClinicFacility
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            Name = trimmed
        };
    }
}

public sealed class DoctorFavourite
{
    private DoctorFavourite()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid DoctorId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DoctorFavourite Create(Guid userId, Guid doctorId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DoctorId = doctorId,
            CreatedAtUtc = now
        };
}
