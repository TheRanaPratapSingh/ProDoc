using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public sealed class FavouriteService(IScheduleStore schedules, IClock clock)
{
    public async Task<IReadOnlyList<DiscoveredDoctor>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ids = await schedules.ListFavouriteDoctorIdsAsync(userId, cancellationToken);
        var doctors = new List<DiscoveredDoctor>();
        foreach (var id in ids)
        {
            var doctor = await schedules.FindDiscoveredDoctorAsync(id, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken);
            if (doctor is not null)
            {
                doctors.Add(doctor);
            }
        }

        return doctors;
    }

    public async Task AddAsync(Guid userId, Guid doctorId, CancellationToken cancellationToken)
    {
        var visible = await schedules.FindDiscoveredDoctorAsync(doctorId, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken);
        if (visible is null)
        {
            throw DomainException.NotFound("doctor_not_found", "That doctor is not available.");
        }

        var existing = await schedules.ListFavouriteDoctorIdsAsync(userId, cancellationToken);
        if (existing.Contains(doctorId))
        {
            return;
        }

        await schedules.AddFavouriteAsync(DoctorFavourite.Create(userId, doctorId, clock.UtcNow), cancellationToken);
        await schedules.SaveChangesAsync(null, cancellationToken);
    }

    public async Task RemoveAsync(Guid userId, Guid doctorId, CancellationToken cancellationToken)
    {
        await schedules.RemoveFavouriteAsync(userId, doctorId, cancellationToken);
        await schedules.SaveChangesAsync(null, cancellationToken);
    }
}
