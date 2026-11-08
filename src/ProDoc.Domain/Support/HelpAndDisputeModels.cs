using ProDoc.Domain.Common;

namespace ProDoc.Domain.Support;

public enum DisputeCaseKind
{
    Appointment = 1,
    Cancellation = 2,
    Payment = 3,
    Refund = 4,
    Complaint = 5
}

public enum DisputeCaseStatus
{
    Open = 1,
    EvidenceRequested = 2,
    UnderReview = 3,
    Escalated = 4,
    Resolved = 5,
    Closed = 6
}

public enum DisputeDecision
{
    None = 0,
    Upheld = 1,
    Denied = 2,
    PartialRefund = 3,
    FullRefund = 4,
    PolicyException = 5
}

public sealed class HelpArticleCategory
{
    private HelpArticleCategory()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string NameHi { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static HelpArticleCategory Create(string code, string nameEn, string nameHi, int sortOrder)
    {
        var normalized = code.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 40)
        {
            throw DomainException.Validation("help_category_invalid", "Enter a valid help category code.");
        }

        return new HelpArticleCategory
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            NameEn = nameEn.Trim(),
            NameHi = nameHi.Trim(),
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void Update(string nameEn, string nameHi, int sortOrder, bool isActive)
    {
        NameEn = nameEn.Trim();
        NameHi = nameHi.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}

public sealed class HelpArticle
{
    private HelpArticle()
    {
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string TitleEn { get; private set; } = string.Empty;

    public string TitleHi { get; private set; } = string.Empty;

    public string BodyEn { get; private set; } = string.Empty;

    public string BodyHi { get; private set; } = string.Empty;

    public string Tags { get; private set; } = string.Empty;

    public bool IsPublished { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static HelpArticle Create(
        Guid categoryId,
        string slug,
        string titleEn,
        string titleHi,
        string bodyEn,
        string bodyHi,
        string? tags,
        Guid actorUserId,
        DateTimeOffset now,
        bool publish = false)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant().Replace(' ', '-');
        if (normalizedSlug.Length is < 3 or > 80)
        {
            throw DomainException.Validation("help_slug_invalid", "Enter a slug from 3 to 80 characters.");
        }

        return new HelpArticle
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Slug = normalizedSlug,
            TitleEn = Required(titleEn, 4, 160),
            TitleHi = Required(titleHi, 4, 160),
            BodyEn = Required(bodyEn, 20, 8000),
            BodyHi = Required(bodyHi, 20, 8000),
            Tags = string.IsNullOrWhiteSpace(tags) ? string.Empty : tags.Trim()[..Math.Min(tags.Trim().Length, 240)],
            IsPublished = publish,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        Guid categoryId,
        string titleEn,
        string titleHi,
        string bodyEn,
        string bodyHi,
        string? tags,
        bool isPublished,
        Guid actorUserId,
        DateTimeOffset now)
    {
        CategoryId = categoryId;
        TitleEn = Required(titleEn, 4, 160);
        TitleHi = Required(titleHi, 4, 160);
        BodyEn = Required(bodyEn, 20, 8000);
        BodyHi = Required(bodyHi, 20, 8000);
        Tags = string.IsNullOrWhiteSpace(tags) ? string.Empty : tags.Trim()[..Math.Min(tags.Trim().Length, 240)];
        IsPublished = isPublished;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
    }

    private static string Required(string value, int min, int max)
    {
        var text = value.Trim();
        if (text.Length < min || text.Length > max)
        {
            throw DomainException.Validation("help_article_invalid", $"Enter text from {min} to {max} characters.");
        }

        return text;
    }
}

public sealed class HelpArticleFeedback
{
    private HelpArticleFeedback()
    {
    }

    public Guid Id { get; private set; }

    public Guid ArticleId { get; private set; }

    public Guid? UserId { get; private set; }

    public bool WasHelpful { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static HelpArticleFeedback Create(Guid articleId, Guid? userId, bool wasHelpful, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ArticleId = articleId,
            UserId = userId,
            WasHelpful = wasHelpful,
            CreatedAtUtc = now
        };
}

public sealed class SupportAttachment
{
    private SupportAttachment()
    {
    }

    public Guid Id { get; private set; }

    public Guid SupportTicketId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SupportAttachment Create(
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        string storageKey,
        long sizeBytes,
        DateTimeOffset now)
    {
        if (sizeBytes is <= 0 or > 5_000_000)
        {
            throw DomainException.Validation("support_attachment_size", "Attachments must be between 1 byte and 5 MB.");
        }

        var name = fileName.Trim();
        if (name.Length is < 1 or > 160)
        {
            throw DomainException.Validation("support_attachment_name", "Enter a valid file name.");
        }

        return new SupportAttachment
        {
            Id = Guid.NewGuid(),
            SupportTicketId = ticketId,
            UploadedByUserId = uploadedByUserId,
            FileName = name,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim()[..Math.Min(contentType.Trim().Length, 100)],
            StorageKey = storageKey.Trim(),
            SizeBytes = sizeBytes,
            CreatedAtUtc = now
        };
    }
}

public sealed class TicketStatusHistory
{
    private TicketStatusHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid SupportTicketId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string FromStatus { get; private set; } = string.Empty;

    public string ToStatus { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public static TicketStatusHistory Record(
        Guid ticketId,
        Guid actorUserId,
        string fromStatus,
        string toStatus,
        string? note,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            SupportTicketId = ticketId,
            ActorUserId = actorUserId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 400)],
            ChangedAtUtc = now
        };
}

public sealed class DisputeCase
{
    private DisputeCase()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid SupportTicketId { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public DisputeCaseKind Kind { get; private set; }

    public DisputeCaseStatus Status { get; private set; }

    public DisputeDecision Decision { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? EvidenceRequest { get; private set; }

    public string? DecisionNotes { get; private set; }

    public Guid? LinkedPaymentId { get; private set; }

    public Guid? LinkedAppointmentId { get; private set; }

    public Guid? RefundId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public static DisputeCase Open(
        string number,
        Guid supportTicketId,
        Guid openedByUserId,
        DisputeCaseKind kind,
        string summary,
        DateTimeOffset now,
        Guid? paymentId = null,
        Guid? appointmentId = null)
    {
        var text = summary.Trim();
        if (text.Length is < 8 or > 2000)
        {
            throw DomainException.Validation("dispute_summary_invalid", "Enter a dispute summary from 8 to 2000 characters.");
        }

        return new DisputeCase
        {
            Id = Guid.NewGuid(),
            Number = number,
            SupportTicketId = supportTicketId,
            OpenedByUserId = openedByUserId,
            Kind = kind,
            Status = DisputeCaseStatus.Open,
            Decision = DisputeDecision.None,
            Summary = text,
            LinkedPaymentId = paymentId,
            LinkedAppointmentId = appointmentId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Assign(Guid agentUserId, DateTimeOffset now)
    {
        AssignedToUserId = agentUserId;
        if (Status == DisputeCaseStatus.Open)
        {
            Status = DisputeCaseStatus.UnderReview;
        }

        UpdatedAtUtc = now;
    }

    public void RequestEvidence(string request, DateTimeOffset now)
    {
        EvidenceRequest = request.Trim();
        Status = DisputeCaseStatus.EvidenceRequested;
        UpdatedAtUtc = now;
    }

    public void Escalate(DateTimeOffset now)
    {
        Status = DisputeCaseStatus.Escalated;
        UpdatedAtUtc = now;
    }

    public void Resolve(DisputeDecision decision, string decisionNotes, Guid? refundId, DateTimeOffset now)
    {
        if (decision == DisputeDecision.None)
        {
            throw DomainException.Validation("dispute_decision_required", "Record a dispute decision.");
        }

        Decision = decision;
        DecisionNotes = decisionNotes.Trim();
        RefundId = refundId;
        Status = DisputeCaseStatus.Resolved;
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void Close(DateTimeOffset now)
    {
        Status = DisputeCaseStatus.Closed;
        UpdatedAtUtc = now;
    }
}
