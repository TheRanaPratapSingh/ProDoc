using ProDoc.Domain.Common;

namespace ProDoc.Domain.Queue;

public sealed class QueueEntry
{
    private QueueEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid QueueSessionId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid DoctorId { get; private set; }

    public DateOnly OperatingDate { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? WalkInRecordId { get; private set; }

    public Guid? AccountUserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string TokenLabel { get; private set; } = string.Empty;

    public int TokenNumber { get; private set; }

    public int QueuePosition { get; private set; }

    public QueueEntryStatus Status { get; private set; }

    public CheckInMethod CheckInMethod { get; private set; }

    public bool IsWalkIn { get; private set; }

    public bool IsLate { get; private set; }

    public DateTimeOffset? AppointmentStartsAtUtc { get; private set; }

    public DateTimeOffset CheckInAtUtc { get; private set; }

    public DateTimeOffset? CalledAtUtc { get; private set; }

    public DateTimeOffset? ConsultationStartedAtUtc { get; private set; }

    public DateTimeOffset? ConsultationCompletedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public bool IsActive => Status is QueueEntryStatus.Waiting or QueueEntryStatus.Called or QueueEntryStatus.InConsultation or QueueEntryStatus.OnHold;

    public static QueueEntry CheckInAppointment(
        ClinicQueueSession session,
        Guid appointmentId,
        Guid? accountUserId,
        string displayName,
        string tokenLabel,
        int tokenNumber,
        int queuePosition,
        CheckInMethod method,
        bool isLate,
        DateTimeOffset? appointmentStartsAtUtc,
        DateTimeOffset now) =>
        Create(
            session,
            appointmentId,
            null,
            accountUserId,
            displayName,
            tokenLabel,
            tokenNumber,
            queuePosition,
            method,
            isWalkIn: false,
            isLate,
            appointmentStartsAtUtc,
            now);

    public static QueueEntry CheckInWalkIn(
        ClinicQueueSession session,
        Guid walkInRecordId,
        string displayName,
        string tokenLabel,
        int tokenNumber,
        int queuePosition,
        DateTimeOffset now) =>
        Create(
            session,
            null,
            walkInRecordId,
            null,
            displayName,
            tokenLabel,
            tokenNumber,
            queuePosition,
            CheckInMethod.WalkIn,
            isWalkIn: true,
            isLate: false,
            null,
            now);

    public QueueStatusHistory Call(Guid actorUserId, DateTimeOffset now) =>
        Transition(QueueEntryStatus.Called, actorUserId, null, now, () => CalledAtUtc = now);

    public QueueStatusHistory Recall(Guid actorUserId, DateTimeOffset now)
    {
        if (Status is not (QueueEntryStatus.Called or QueueEntryStatus.Skipped or QueueEntryStatus.OnHold))
        {
            throw DomainException.Conflict("queue_transition_invalid", "That patient cannot be recalled right now.");
        }

        return Transition(QueueEntryStatus.Called, actorUserId, null, now, () => CalledAtUtc = now);
    }

    public QueueStatusHistory StartConsultation(Guid actorUserId, DateTimeOffset now)
    {
        if (Status is not (QueueEntryStatus.Called or QueueEntryStatus.Waiting))
        {
            throw DomainException.Conflict("queue_transition_invalid", "Start consultation from a called or waiting patient.");
        }

        return Transition(QueueEntryStatus.InConsultation, actorUserId, null, now, () =>
        {
            CalledAtUtc ??= now;
            ConsultationStartedAtUtc = now;
        });
    }

    public QueueStatusHistory Complete(Guid actorUserId, DateTimeOffset now) =>
        Transition(QueueEntryStatus.Completed, actorUserId, null, now, () =>
        {
            ConsultationStartedAtUtc ??= now;
            ConsultationCompletedAtUtc = now;
        });

    public QueueStatusHistory Skip(Guid actorUserId, string? reason, DateTimeOffset now) =>
        Transition(QueueEntryStatus.Skipped, actorUserId, reason, now);

    public QueueStatusHistory Hold(Guid actorUserId, string? reason, DateTimeOffset now) =>
        Transition(QueueEntryStatus.OnHold, actorUserId, reason, now);

    public QueueStatusHistory Resume(Guid actorUserId, DateTimeOffset now)
    {
        if (Status != QueueEntryStatus.OnHold)
        {
            throw DomainException.Conflict("queue_transition_invalid", "Only a held patient can be resumed.");
        }

        return Transition(QueueEntryStatus.Waiting, actorUserId, null, now);
    }

    public QueueStatusHistory Cancel(Guid actorUserId, string? reason, DateTimeOffset now) =>
        Transition(QueueEntryStatus.Cancelled, actorUserId, reason, now);

    public void SetPosition(int position, DateTimeOffset now)
    {
        if (position < 1)
        {
            throw DomainException.Validation("queue_position_invalid", "Queue position must be at least 1.");
        }

        QueuePosition = position;
        UpdatedAtUtc = now;
    }

    private QueueStatusHistory Transition(
        QueueEntryStatus next,
        Guid actorUserId,
        string? reason,
        DateTimeOffset now,
        Action? sideEffect = null)
    {
        EnsureAllowed(next);
        var previous = Status;
        Status = next;
        sideEffect?.Invoke();
        UpdatedAtUtc = now;
        return QueueStatusHistory.Record(Id, previous, next, actorUserId, reason, now);
    }

    private void EnsureAllowed(QueueEntryStatus next)
    {
        var allowed = Status switch
        {
            QueueEntryStatus.Waiting => next is QueueEntryStatus.Called or QueueEntryStatus.InConsultation or QueueEntryStatus.OnHold or QueueEntryStatus.Skipped or QueueEntryStatus.Cancelled,
            QueueEntryStatus.Called => next is QueueEntryStatus.InConsultation or QueueEntryStatus.Skipped or QueueEntryStatus.OnHold or QueueEntryStatus.Cancelled or QueueEntryStatus.Called,
            QueueEntryStatus.InConsultation => next is QueueEntryStatus.Completed or QueueEntryStatus.OnHold or QueueEntryStatus.Cancelled,
            QueueEntryStatus.OnHold => next is QueueEntryStatus.Waiting or QueueEntryStatus.Called or QueueEntryStatus.Cancelled,
            QueueEntryStatus.Skipped => next is QueueEntryStatus.Called or QueueEntryStatus.Cancelled,
            _ => false
        };
        if (!allowed)
        {
            throw DomainException.Conflict("queue_transition_invalid", "That queue action is not allowed from the current status.");
        }
    }

    private static QueueEntry Create(
        ClinicQueueSession session,
        Guid? appointmentId,
        Guid? walkInRecordId,
        Guid? accountUserId,
        string displayName,
        string tokenLabel,
        int tokenNumber,
        int queuePosition,
        CheckInMethod method,
        bool isWalkIn,
        bool isLate,
        DateTimeOffset? appointmentStartsAtUtc,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            QueueSessionId = session.Id,
            ClinicId = session.ClinicId,
            BranchId = session.BranchId,
            DoctorId = session.DoctorId,
            OperatingDate = session.OperatingDate,
            AppointmentId = appointmentId,
            WalkInRecordId = walkInRecordId,
            AccountUserId = accountUserId,
            DisplayName = displayName.Trim(),
            TokenLabel = tokenLabel,
            TokenNumber = tokenNumber,
            QueuePosition = queuePosition,
            Status = QueueEntryStatus.Waiting,
            CheckInMethod = method,
            IsWalkIn = isWalkIn,
            IsLate = isLate,
            AppointmentStartsAtUtc = appointmentStartsAtUtc,
            CheckInAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
}

public sealed class QueueStatusHistory
{
    private QueueStatusHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid QueueEntryId { get; private set; }

    public QueueEntryStatus PreviousStatus { get; private set; }

    public QueueEntryStatus NewStatus { get; private set; }

    public Guid ChangedByAccountId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public static QueueStatusHistory Record(
        Guid queueEntryId,
        QueueEntryStatus previous,
        QueueEntryStatus next,
        Guid actorUserId,
        string? reason,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            QueueEntryId = queueEntryId,
            PreviousStatus = previous,
            NewStatus = next,
            ChangedByAccountId = actorUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 120)],
            ChangedAtUtc = now
        };
}
