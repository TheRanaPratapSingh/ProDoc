using Microsoft.EntityFrameworkCore;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Providers;

public sealed class ProviderSeeder(ProDocDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Specializations.Select(item => item.Code).ToListAsync(cancellationToken);
        foreach (var item in SpecializationCatalog.All)
        {
            if (!existing.Contains(item.Code, StringComparer.Ordinal))
            {
                db.Specializations.Add(Specialization.Create(item.Id, item.Code, item.Name));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
