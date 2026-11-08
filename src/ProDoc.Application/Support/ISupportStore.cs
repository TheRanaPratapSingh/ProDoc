using ProDoc.Domain.Admin;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Support;

namespace ProDoc.Application.Support;

public interface ISupportStore
{
    Task<IReadOnlyList<HelpArticleCategory>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<HelpArticleCategory?> FindCategoryAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<HelpArticleCategory?> FindCategoryByCodeAsync(string code, CancellationToken cancellationToken);

    void AddCategory(HelpArticleCategory category);

    Task<IReadOnlyList<HelpArticle>> SearchArticlesAsync(string? query, Guid? categoryId, bool publishedOnly, int take, CancellationToken cancellationToken);

    Task<HelpArticle?> FindArticleAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<HelpArticle?> FindArticleBySlugAsync(string slug, CancellationToken cancellationToken);

    void AddArticle(HelpArticle article);

    void AddArticleFeedback(HelpArticleFeedback feedback);

    Task<IReadOnlyList<Guid>> ListPlatformAdminUserIdsAsync(CancellationToken cancellationToken);

    Task<bool> HasRecentSimilarTicketAsync(Guid userId, string subject, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<SupportTicket?> FindTicketAsync(Guid ticketId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportTicket>> QueryTicketsAsync(
        Guid? createdByUserId,
        Guid? assignedToUserId,
        SupportTicketStatus? status,
        SupportTicketPriority? minPriority,
        bool overdueOnly,
        bool unassignedOnly,
        int take,
        CancellationToken cancellationToken);

    void AddTicket(SupportTicket ticket);

    void AddTicketNote(SupportTicketNote note);

    Task<string> NextTicketNumberAsync(CancellationToken cancellationToken);

    Task AddStatusHistoryAsync(TicketStatusHistory history, CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketStatusHistory>> ListStatusHistoryAsync(Guid ticketId, CancellationToken cancellationToken);

    void AddAttachment(SupportAttachment attachment);

    Task<SupportAttachment?> FindAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportAttachment>> ListAttachmentsAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<DisputeCase?> FindDisputeAsync(Guid disputeId, bool tracking, CancellationToken cancellationToken);

    Task<DisputeCase?> FindDisputeByTicketAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DisputeCase>> ListDisputesAsync(DisputeCaseStatus? status, int take, CancellationToken cancellationToken);

    void AddDispute(DisputeCase dispute);

    Task<string> NextDisputeNumberAsync(CancellationToken cancellationToken);

    Task<SupportAnalytics> BuildAnalyticsAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ISupportFileStore
{
    Task<string> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}
