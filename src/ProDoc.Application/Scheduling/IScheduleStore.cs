using ProDoc.Domain.Governance;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public interface IScheduleStore
{
    Task<ConsultationSchedule?> FindAsync(Guid scheduleId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationSchedule>> ListForDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationSchedule>> ListForClinicsAsync(IReadOnlyCollection<Guid> clinicIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationSchedule>> ListPublishedForDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task AddAsync(ConsultationSchedule schedule, CancellationToken cancellationToken);

    void Remove(ConsultationSchedule schedule);

    /// <summary>
    /// Pins optimistic concurrency to the client's known version so overlapping edits conflict instead of silently overwriting.
    /// </summary>
    void ApplyExpectedRowVersion(ConsultationSchedule schedule, byte[] expectedRowVersion);

    Task<bool> ExistsAsync(Guid doctorId, Guid? clinicId, Guid? branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DiscoveryPlace>> SearchAsync(DiscoveryQuery query, DateOnly today, CancellationToken cancellationToken);

    Task<DiscoveredDoctor?> FindDiscoveredDoctorAsync(Guid doctorId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<DiscoveredClinic>> SearchClinicsAsync(DiscoveryQuery query, DateOnly today, CancellationToken cancellationToken);

    Task<DiscoveredClinic?> FindDiscoveredClinicAsync(Guid clinicId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(string text, CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchOperatingHour>> ListOperatingHoursAsync(Guid branchId, CancellationToken cancellationToken);

    Task ReplaceOperatingHoursAsync(Guid branchId, IReadOnlyList<BranchOperatingHour> hours, CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchClosure>> ListBranchClosuresAsync(Guid branchId, CancellationToken cancellationToken);

    Task AddBranchClosureAsync(BranchClosure closure, CancellationToken cancellationToken);

    Task<bool> RemoveBranchClosureAsync(Guid branchId, Guid closureId, CancellationToken cancellationToken);

    Task ReplaceFacilitiesAsync(Guid clinicId, IReadOnlyList<ClinicFacility> facilities, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListFavouriteDoctorIdsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddFavouriteAsync(DoctorFavourite favourite, CancellationToken cancellationToken);

    Task<bool> RemoveFavouriteAsync(Guid userId, Guid doctorId, CancellationToken cancellationToken);

    Task SaveChangesAsync(AuditLog? audit, CancellationToken cancellationToken);
}
