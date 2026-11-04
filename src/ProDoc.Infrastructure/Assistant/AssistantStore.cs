using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Assistant;
using ProDoc.Domain.Assistant;
using ProDoc.Domain.Governance;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Assistant;

public sealed class AssistantStore(ProDocDbContext db) : IAssistantStore
{
    public Task<AssistantConversation?> FindConversationAsync(Guid conversationId, Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AssistantConversations.AsQueryable() : db.AssistantConversations.AsNoTracking();
        return query
            .Include(item => item.Messages)
            .SingleOrDefaultAsync(item => item.Id == conversationId && item.UserId == userId && item.DeletedAtUtc == null, cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        await db.AssistantConversations.AsNoTracking()
            .Where(item => item.UserId == userId && item.DeletedAtUtc == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void AddConversation(AssistantConversation conversation) => db.AssistantConversations.Add(conversation);

    public void AddMessage(AssistantMessage message) => db.AssistantMessages.Add(message);

    public Task<AssistantConsent?> FindConsentAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AssistantConsents.AsQueryable() : db.AssistantConsents.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddConsent(AssistantConsent consent) => db.AssistantConsents.Add(consent);

    public Task<AssistantUsageDaily?> FindUsageAsync(Guid userId, DateOnly date, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AssistantUsageDaily.AsQueryable() : db.AssistantUsageDaily.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId && item.UsageDate == date, cancellationToken);
    }

    public void AddUsage(AssistantUsageDaily usage) => db.AssistantUsageDaily.Add(usage);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
