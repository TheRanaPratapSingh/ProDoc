using ProDoc.Domain.Common;

namespace ProDoc.Domain.Admin;

public enum SupportTicketStatus
{
    Open = 1,
    InProgress = 2,
    WaitingOnUser = 3,
    Resolved = 4,
    Closed = 5
}

public enum SupportTicketPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum SupportTicketCategory
{
    PatientComplaint = 1,
    ProviderComplaint = 2,
    BookingDispute = 3,
    PaymentIssue = 4,
    Other = 5,
    AppointmentIssue = 6,
    AccountAccess = 7,
    TechnicalProblem = 8,
    PrescriptionAccess = 9,
    CancellationDispute = 10,
    RefundRequest = 11
}

public sealed class SupportTicket
{
    private readonly List<SupportTicketNote> _notes = [];

    private SupportTicket()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public SupportTicketCategory Category { get; private set; }

    public SupportTicketPriority Priority { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid? AppointmentId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public DateTimeOffset? EscalatedAtUtc { get; private set; }

    public DateTimeOffset? DueAtUtc { get; private set; }

    public string? ResolutionDetails { get; private set; }

    public IReadOnlyCollection<SupportTicketNote> Notes => _notes;

    public static SupportTicket Create(
        string number,
        Guid createdByUserId,
        SupportTicketCategory category,
        SupportTicketPriority priority,
        string subject,
        string description,
        DateTimeOffset now,
        Guid? appointmentId = null,
        Guid? paymentId = null,
        Guid? clinicId = null)
    {
        var subj = subject.Trim();
        var body = description.Trim();
        if (subj.Length is < 4 or > 160)
        {
            throw DomainException.Validation("ticket_subject_invalid", "Enter a subject from 4 to 160 characters.");
        }

        if (body.Length is < 8 or > 4000)
        {
            throw DomainException.Validation("ticket_description_invalid", "Enter a description from 8 to 4000 characters.");
        }

        return new SupportTicket
        {
            Id = Guid.NewGuid(),
            Number = number,
            CreatedByUserId = createdByUserId,
            Category = category,
            Priority = priority,
            Status = SupportTicketStatus.Open,
            Subject = subj,
            Description = body,
            AppointmentId = appointmentId,
            PaymentId = paymentId,
            ClinicId = clinicId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            DueAtUtc = now.AddHours(priority is SupportTicketPriority.Urgent or SupportTicketPriority.High ? 24 : 72)
        };
    }

    public void Assign(Guid? adminUserId, DateTimeOffset now)
    {
        AssignedToUserId = adminUserId;
        if (adminUserId is not null && Status == SupportTicketStatus.Open)
        {
            Status = SupportTicketStatus.InProgress;
        }

        UpdatedAtUtc = now;
    }

    public void SetPriority(SupportTicketPriority priority, DateTimeOffset now)
    {
        Priority = priority;
        DueAtUtc = now.AddHours(priority is SupportTicketPriority.Urgent or SupportTicketPriority.High ? 24 : 72);
        UpdatedAtUtc = now;
    }

    public void UpdateStatus(SupportTicketStatus status, DateTimeOffset now, string? resolutionDetails = null)
    {
        Status = status;
        if (status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed)
        {
            ResolvedAtUtc ??= now;
            if (!string.IsNullOrWhiteSpace(resolutionDetails))
            {
                ResolutionDetails = resolutionDetails.Trim()[..Math.Min(resolutionDetails.Trim().Length, 2000)];
            }
        }

        if (status == SupportTicketStatus.Closed)
        {
            ClosedAtUtc ??= now;
        }

        UpdatedAtUtc = now;
    }

    public void Escalate(DateTimeOffset now)
    {
        EscalatedAtUtc = now;
        if (Priority < SupportTicketPriority.High)
        {
            Priority = SupportTicketPriority.High;
        }

        DueAtUtc = now.AddHours(12);
        UpdatedAtUtc = now;
    }

    public SupportTicketNote AddNote(Guid authorUserId, string body, bool isInternal, DateTimeOffset now)
    {
        var note = SupportTicketNote.Create(Id, authorUserId, body, isInternal, now);
        _notes.Add(note);
        UpdatedAtUtc = now;
        return note;
    }
}

public sealed class SupportTicketNote
{
    private SupportTicketNote()
    {
    }

    public Guid Id { get; private set; }

    public Guid SupportTicketId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public bool IsInternal { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SupportTicketNote Create(Guid ticketId, Guid authorUserId, string body, bool isInternal, DateTimeOffset now)
    {
        var text = body.Trim();
        if (text.Length is < 2 or > 4000)
        {
            throw DomainException.Validation("ticket_note_invalid", "Enter a note from 2 to 4000 characters.");
        }

        return new SupportTicketNote
        {
            Id = Guid.NewGuid(),
            SupportTicketId = ticketId,
            AuthorUserId = authorUserId,
            Body = text,
            IsInternal = isInternal,
            CreatedAtUtc = now
        };
    }
}
