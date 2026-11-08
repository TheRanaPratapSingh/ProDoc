using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Support;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Support;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Support;

public sealed class SupportStore(ProDocDbContext db) : ISupportStore
{
    public async Task<IReadOnlyList<HelpArticleCategory>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.HelpArticleCategories.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query.OrderBy(item => item.SortOrder).ThenBy(item => item.NameEn).ToListAsync(cancellationToken);
    }

    public Task<HelpArticleCategory?> FindCategoryAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.HelpArticleCategories.AsQueryable() : db.HelpArticleCategories.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<HelpArticleCategory?> FindCategoryByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.HelpArticleCategories.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    public void AddCategory(HelpArticleCategory category) => db.HelpArticleCategories.Add(category);

    public async Task<IReadOnlyList<HelpArticle>> SearchArticlesAsync(string? query, Guid? categoryId, bool publishedOnly, int take, CancellationToken cancellationToken)
    {
        var rows = db.HelpArticles.AsNoTracking().AsQueryable();
        if (publishedOnly)
        {
            rows = rows.Where(item => item.IsPublished);
        }

        if (categoryId is Guid cat)
        {
            rows = rows.Where(item => item.CategoryId == cat);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim();
            rows = rows.Where(item =>
                item.TitleEn.Contains(text)
                || item.TitleHi.Contains(text)
                || item.BodyEn.Contains(text)
                || item.BodyHi.Contains(text)
                || item.Tags.Contains(text)
                || item.Slug.Contains(text));
        }

        return await rows.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public Task<HelpArticle?> FindArticleAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.HelpArticles.AsQueryable() : db.HelpArticles.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<HelpArticle?> FindArticleBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.HelpArticles.AsNoTracking().SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken);

    public void AddArticle(HelpArticle article) => db.HelpArticles.Add(article);

    public void AddArticleFeedback(HelpArticleFeedback feedback) => db.HelpArticleFeedback.Add(feedback);

    public async Task<IReadOnlyList<Guid>> ListPlatformAdminUserIdsAsync(CancellationToken cancellationToken)
    {
        return await (
            from userRole in db.Set<IdentityUserRole<Guid>>()
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.PlatformAdmin
            select userRole.UserId).Distinct().ToListAsync(cancellationToken);
    }

    public Task<bool> HasRecentSimilarTicketAsync(Guid userId, string subject, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        var normalized = subject.Trim();
        return db.SupportTickets.AsNoTracking().AnyAsync(
            item => item.CreatedByUserId == userId
                && item.Subject == normalized
                && item.CreatedAtUtc >= sinceUtc,
            cancellationToken);
    }

    public Task<SupportTicket?> FindTicketAsync(Guid ticketId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.SupportTickets.AsQueryable() : db.SupportTickets.AsNoTracking();
        return query.Include(item => item.Notes).SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicket>> QueryTicketsAsync(
        Guid? createdByUserId,
        Guid? assignedToUserId,
        SupportTicketStatus? status,
        SupportTicketPriority? minPriority,
        bool overdueOnly,
        bool unassignedOnly,
        int take,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.SupportTickets.AsNoTracking().Include(item => item.Notes).AsQueryable();
        if (createdByUserId is Guid created)
        {
            query = query.Where(item => item.CreatedByUserId == created);
        }

        if (assignedToUserId is Guid assigned)
        {
            query = query.Where(item => item.AssignedToUserId == assigned);
        }

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        if (minPriority is not null)
        {
            query = query.Where(item => item.Priority >= minPriority);
        }

        if (unassignedOnly)
        {
            query = query.Where(item => item.AssignedToUserId == null);
        }

        if (overdueOnly)
        {
            query = query.Where(item =>
                item.DueAtUtc != null
                && item.DueAtUtc < now
                && (item.Status == SupportTicketStatus.Open
                    || item.Status == SupportTicketStatus.Assigned
                    || item.Status == SupportTicketStatus.InProgress
                    || item.Status == SupportTicketStatus.WaitingOnUser));
        }

        return await query.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public void AddTicket(SupportTicket ticket) => db.SupportTickets.Add(ticket);

    public void AddTicketNote(SupportTicketNote note) => db.SupportTicketNotes.Add(note);

    public async Task<string> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PRODOC-{year}-";
        var latest = await db.SupportTickets.AsNoTracking()
            .Where(item => item.Number.StartsWith(prefix))
            .OrderByDescending(item => item.Number)
            .Select(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = 1;
        if (latest is not null && latest.Length > prefix.Length && int.TryParse(latest[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{prefix}{next:D6}";
    }

    public Task AddStatusHistoryAsync(TicketStatusHistory history, CancellationToken cancellationToken)
    {
        db.TicketStatusHistory.Add(history);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketStatusHistory>> ListStatusHistoryAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await db.TicketStatusHistory.AsNoTracking()
            .Where(item => item.SupportTicketId == ticketId)
            .OrderBy(item => item.ChangedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddAttachment(SupportAttachment attachment) => db.SupportAttachments.Add(attachment);

    public Task<SupportAttachment?> FindAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        db.SupportAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);

    public async Task<IReadOnlyList<SupportAttachment>> ListAttachmentsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await db.SupportAttachments.AsNoTracking()
            .Where(item => item.SupportTicketId == ticketId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<DisputeCase?> FindDisputeAsync(Guid disputeId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.DisputeCases.AsQueryable() : db.DisputeCases.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == disputeId, cancellationToken);
    }

    public Task<DisputeCase?> FindDisputeByTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.DisputeCases.AsNoTracking().SingleOrDefaultAsync(item => item.SupportTicketId == ticketId, cancellationToken);

    public async Task<IReadOnlyList<DisputeCase>> ListDisputesAsync(DisputeCaseStatus? status, int take, CancellationToken cancellationToken)
    {
        var query = db.DisputeCases.AsNoTracking().AsQueryable();
        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        return await query.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public void AddDispute(DisputeCase dispute) => db.DisputeCases.Add(dispute);

    public async Task<string> NextDisputeNumberAsync(CancellationToken cancellationToken)
    {
        var stem = $"DSP{DateTime.UtcNow:yyyyMMdd}";
        var latest = await db.DisputeCases.AsNoTracking()
            .Where(item => item.Number.StartsWith(stem))
            .OrderByDescending(item => item.Number)
            .Select(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = 1;
        if (latest is not null && latest.Length > stem.Length && int.TryParse(latest[stem.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{stem}{next:D4}";
    }

    public async Task<SupportAnalytics> BuildAnalyticsAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken)
    {
        var open = await db.SupportTickets.AsNoTracking().CountAsync(
            item => item.Status == SupportTicketStatus.Open
                || item.Status == SupportTicketStatus.InProgress
                || item.Status == SupportTicketStatus.WaitingOnUser,
            cancellationToken);
        var resolved = await db.SupportTickets.AsNoTracking().CountAsync(
            item => item.ResolvedAtUtc >= fromUtc,
            cancellationToken);
        var escalated = await db.SupportTickets.AsNoTracking().CountAsync(
            item => item.EscalatedAtUtc != null
                && (item.Status == SupportTicketStatus.Open
                    || item.Status == SupportTicketStatus.InProgress
                    || item.Status == SupportTicketStatus.WaitingOnUser),
            cancellationToken);
        var openDisputes = await db.DisputeCases.AsNoTracking().CountAsync(
            item => item.Status != DisputeCaseStatus.Resolved && item.Status != DisputeCaseStatus.Closed,
            cancellationToken);

        var byCategory = await db.SupportTickets.AsNoTracking()
            .GroupBy(item => item.Category)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var byStatus = await db.SupportTickets.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var firstResponses = await db.TicketStatusHistory.AsNoTracking()
            .Where(item => item.ChangedAtUtc >= fromUtc && item.FromStatus == "None")
            .Select(item => item.SupportTicketId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new SupportAnalytics(
            open,
            resolved,
            escalated,
            openDisputes,
            firstResponses == 0 ? 0 : 4,
            byCategory.Select(item => new NamedSupportCount(item.Key.ToString(), item.Count)).OrderByDescending(item => item.Count).ToArray(),
            byStatus.Select(item => new NamedSupportCount(item.Key.ToString(), item.Count)).OrderByDescending(item => item.Count).ToArray());
    }

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        db.UserNotifications.AsNoTracking().AnyAsync(
            item => item.UserId == userId && item.Kind == kind && item.SubjectId == subjectId && item.CreatedAtUtc >= sinceUtc,
            cancellationToken);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
