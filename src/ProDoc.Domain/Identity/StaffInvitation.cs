using ProDoc.Domain.Common;

namespace ProDoc.Domain.Identity;

public enum StaffInvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Cancelled = 3,
    Expired = 4
}

public sealed class StaffInvitation
{
    private StaffInvitation()
    {
    }

    public Guid Id { get; private set; }

    public string PhoneE164 { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string RoleName { get; private set; } = string.Empty;

    public Guid? ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    /// <summary>JSON payload for role-specific invite fields (specialization, clinics, doctors, designation).</summary>
    public string? PayloadJson { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public StaffInvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public Guid? AcceptedUserId { get; private set; }

    public static StaffInvitation Create(
        string phoneE164,
        string fullName,
        string? email,
        string roleName,
        Guid? clinicId,
        Guid? branchId,
        Guid invitedByUserId,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? payloadJson = null)
    {
        if (string.IsNullOrWhiteSpace(phoneE164))
        {
            throw new ArgumentException("Phone is required.", nameof(phoneE164));
        }

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 80)
        {
            throw DomainException.Validation("invite_name_invalid", "Enter a full name up to 80 characters.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        return new StaffInvitation
        {
            Id = Guid.NewGuid(),
            PhoneE164 = phoneE164,
            FullName = fullName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            RoleName = roleName,
            ClinicId = clinicId,
            BranchId = branchId,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? null : payloadJson.Trim(),
            InvitedByUserId = invitedByUserId,
            Status = StaffInvitationStatus.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime)
        };
    }

    public bool IsPending(DateTimeOffset now) =>
        Status == StaffInvitationStatus.Pending && ExpiresAtUtc > now;

    public void EnsureAcceptable(DateTimeOffset now)
    {
        if (Status == StaffInvitationStatus.Accepted)
        {
            throw DomainException.Conflict("invite_already_accepted", "This invitation was already accepted.");
        }

        if (Status == StaffInvitationStatus.Cancelled)
        {
            throw DomainException.Validation("invite_cancelled", "This invitation was cancelled.");
        }

        if (ExpiresAtUtc <= now || Status == StaffInvitationStatus.Expired)
        {
            Status = StaffInvitationStatus.Expired;
            throw DomainException.Validation("invite_expired", "This invitation has expired. Ask an admin to resend it.");
        }
    }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        EnsureAcceptable(now);
        Status = StaffInvitationStatus.Accepted;
        AcceptedAtUtc = now;
        AcceptedUserId = userId;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == StaffInvitationStatus.Accepted)
        {
            throw DomainException.Conflict("invite_already_accepted", "This invitation was already accepted.");
        }

        Status = StaffInvitationStatus.Cancelled;
        ExpiresAtUtc = now < ExpiresAtUtc ? now : ExpiresAtUtc;
    }

    public void Extend(DateTimeOffset now, TimeSpan lifetime)
    {
        if (Status is StaffInvitationStatus.Accepted or StaffInvitationStatus.Cancelled)
        {
            throw DomainException.Conflict("invite_not_pending", "Only pending invitations can be resent.");
        }

        Status = StaffInvitationStatus.Pending;
        ExpiresAtUtc = now.Add(lifetime);
    }
}
