using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public sealed class ClinicOperationsService(IScheduleStore schedules, IProviderStore providers)
{
    public async Task<BranchOperations> GetAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(userId, clinicId, branchId, cancellationToken);
        return await ReadAsync(branchId, cancellationToken);
    }

    public async Task<BranchOperations> ReplaceHoursAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        IReadOnlyCollection<(DayOfWeek Day, TimeOnly OpensAt, TimeOnly ClosesAt, bool IsClosed)> periods,
        CancellationToken cancellationToken)
    {
        await AuthorizeAsync(userId, clinicId, branchId, cancellationToken);
        var hours = periods.Select(period => BranchOperatingHour.Create(branchId, period.Day, period.OpensAt, period.ClosesAt, period.IsClosed)).ToArray();
        await schedules.ReplaceOperatingHoursAsync(branchId, hours, cancellationToken);
        await schedules.SaveChangesAsync(cancellationToken);
        return await ReadAsync(branchId, cancellationToken);
    }

    public async Task<BranchOperations> AddClosureAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        DateOnly date,
        string? reason,
        CancellationToken cancellationToken)
    {
        await AuthorizeAsync(userId, clinicId, branchId, cancellationToken);
        var existing = await schedules.ListBranchClosuresAsync(branchId, cancellationToken);
        if (existing.Any(item => item.Date == date))
        {
            throw DomainException.Conflict("closure_exists", "That date is already closed.");
        }

        await schedules.AddBranchClosureAsync(BranchClosure.Create(branchId, date, reason), cancellationToken);
        await schedules.SaveChangesAsync(cancellationToken);
        return await ReadAsync(branchId, cancellationToken);
    }

    public async Task<BranchOperations> RemoveClosureAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid closureId,
        CancellationToken cancellationToken)
    {
        await AuthorizeAsync(userId, clinicId, branchId, cancellationToken);
        if (!await schedules.RemoveBranchClosureAsync(branchId, closureId, cancellationToken))
        {
            throw DomainException.NotFound("closure_not_found", "That closure was not found.");
        }

        await schedules.SaveChangesAsync(cancellationToken);
        return await ReadAsync(branchId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ReplaceFacilitiesAsync(
        Guid userId,
        Guid clinicId,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId != userId)
        {
            throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        }

        var distinct = names
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .Select(name => ClinicFacility.Create(clinicId, name))
            .ToArray();
        await schedules.ReplaceFacilitiesAsync(clinicId, distinct, cancellationToken);
        await schedules.SaveChangesAsync(cancellationToken);
        return distinct.Select(item => item.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task AuthorizeAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId != userId || clinic.Branches.All(branch => branch.Id != branchId))
        {
            throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        }
    }

    private async Task<BranchOperations> ReadAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var hours = await schedules.ListOperatingHoursAsync(branchId, cancellationToken);
        var closures = await schedules.ListBranchClosuresAsync(branchId, cancellationToken);
        return new BranchOperations(
            branchId,
            hours.OrderBy(item => item.Day).ThenBy(item => item.OpensAt)
                .Select(item => new OperatingHourModel(item.Id, item.Day, item.OpensAt, item.ClosesAt, item.IsClosed))
                .ToArray(),
            closures.OrderBy(item => item.Date).Select(item => new ClosureModel(item.Id, item.Date, item.Reason)).ToArray());
    }
}
