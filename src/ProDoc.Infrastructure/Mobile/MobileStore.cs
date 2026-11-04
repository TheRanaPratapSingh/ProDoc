using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Mobile;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Mobile;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Mobile;

public sealed class MobileStore(ProDocDbContext db) : IMobileStore
{
    public Task<PushDevice?> FindByTokenAsync(string token, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PushDevices.AsQueryable() : db.PushDevices.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Token == token, cancellationToken);
    }

    public async Task<IReadOnlyList<PushDevice>> ListActiveForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.PushDevices.AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(PushDevice device) => db.PushDevices.Add(device);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
