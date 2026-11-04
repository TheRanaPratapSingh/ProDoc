namespace ProDoc.Application.Support;

public sealed record HelpCategoryDetails(Guid Id, string Code, string NameEn, string NameHi, int SortOrder, bool IsActive);

public sealed record HelpArticleSummary(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string Slug,
    string TitleEn,
    string TitleHi,
    string Tags,
    bool IsPublished,
    DateTimeOffset UpdatedAtUtc);

public sealed record HelpArticleDetails(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string Slug,
    string TitleEn,
    string TitleHi,
    string BodyEn,
    string BodyHi,
    string Tags,
    bool IsPublished,
    DateTimeOffset UpdatedAtUtc);

public sealed record SupportAttachmentDetails(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record TicketStatusHistoryDetails(
    Guid Id,
    string FromStatus,
    string ToStatus,
    Guid ActorUserId,
    string? Note,
    DateTimeOffset ChangedAtUtc);

public sealed record AgentTicketWorkspace(
    int OpenCount,
    int PendingCount,
    int HighPriorityCount,
    int AssignedToMeCount,
    int OverdueCount,
    IReadOnlyList<ProDoc.Application.Admin.SupportTicketSummary> Tickets);

public sealed record SupportAnalytics(
    int OpenTickets,
    int ResolvedLast30Days,
    int EscalatedOpen,
    int OpenDisputes,
    int AverageFirstResponseHours,
    IReadOnlyList<NamedSupportCount> ByCategory,
    IReadOnlyList<NamedSupportCount> ByStatus);

public sealed record NamedSupportCount(string Label, int Count);

public sealed record DisputeCaseSummary(
    Guid Id,
    string Number,
    Guid SupportTicketId,
    string Kind,
    string Status,
    string Decision,
    string Summary,
    Guid? AssignedToUserId,
    Guid? LinkedPaymentId,
    Guid? LinkedAppointmentId,
    Guid? RefundId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record DisputeCaseDetails(
    Guid Id,
    string Number,
    Guid SupportTicketId,
    string TicketNumber,
    string Kind,
    string Status,
    string Decision,
    string Summary,
    string? EvidenceRequest,
    string? DecisionNotes,
    Guid OpenedByUserId,
    Guid? AssignedToUserId,
    Guid? LinkedPaymentId,
    Guid? LinkedAppointmentId,
    Guid? RefundId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);
