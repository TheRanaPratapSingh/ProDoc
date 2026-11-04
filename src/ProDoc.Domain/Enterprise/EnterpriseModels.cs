using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Enterprise;

public enum OrganizationKind
{
    HealthcareGroup = 1,
    Hospital = 2,
    ClinicChain = 3
}

public enum OrganizationMemberRole
{
    OrganizationOwner = 1,
    OrganizationAdmin = 2,
    HospitalAdministrator = 3,
    BranchManager = 4,
    FinanceManager = 5,
    Doctor = 6,
    Receptionist = 7,
    Staff = 8
}

public enum OrganizationSubscriptionStatus
{
    Trial = 1,
    Active = 2,
    PastDue = 3,
    Cancelled = 4,
    Suspended = 5
}

public enum ResourceKind
{
    ConsultationRoom = 1,
    Equipment = 2
}

public static class OrganizationRoles
{
    public static bool CanManageOrganization(OrganizationMemberRole role) =>
        role is OrganizationMemberRole.OrganizationOwner
            or OrganizationMemberRole.OrganizationAdmin
            or OrganizationMemberRole.HospitalAdministrator;

    public static bool CanManageStaff(OrganizationMemberRole role) =>
        CanManageOrganization(role) || role is OrganizationMemberRole.BranchManager;

    public static bool CanViewAnalytics(OrganizationMemberRole role) =>
        CanManageOrganization(role)
        || role is OrganizationMemberRole.BranchManager
            or OrganizationMemberRole.FinanceManager;

    public static bool CanManageBilling(OrganizationMemberRole role) =>
        role is OrganizationMemberRole.OrganizationOwner
            or OrganizationMemberRole.OrganizationAdmin
            or OrganizationMemberRole.FinanceManager;

    public static bool CanManageBranch(OrganizationMemberRole role, Guid? membershipBranchId, Guid branchId) =>
        CanManageOrganization(role)
        || (role == OrganizationMemberRole.BranchManager && (membershipBranchId is null || membershipBranchId == branchId));
}

public sealed class Organization
{
    private Organization()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public OrganizationKind Kind { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Organization Create(string name, OrganizationKind kind, Guid createdByUserId, DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("organization_name_invalid", "Enter an organization name between 2 and 160 characters.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw DomainException.Validation("organization_kind_invalid", "Choose a valid organization kind.");
        }

        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = text,
            Kind = kind,
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Rename(string name, DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("organization_name_invalid", "Enter an organization name between 2 and 160 characters.");
        }

        Name = text;
        UpdatedAtUtc = now;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAtUtc = now;
    }
}

public sealed class OrganizationMembership
{
    private OrganizationMembership()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public OrganizationMemberRole Role { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrganizationMembership Assign(
        Guid organizationId,
        Guid userId,
        OrganizationMemberRole role,
        Guid? clinicId,
        Guid? branchId,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw DomainException.Validation("org_role_invalid", "Choose a valid organization role.");
        }

        if (branchId is not null && clinicId is null)
        {
            throw DomainException.Validation("org_scope_invalid", "A branch scope requires a clinic.");
        }

        return new OrganizationMembership
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            ClinicId = clinicId,
            BranchId = branchId,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateRole(OrganizationMemberRole role, Guid? clinicId, Guid? branchId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw DomainException.Validation("org_role_invalid", "Choose a valid organization role.");
        }

        if (branchId is not null && clinicId is null)
        {
            throw DomainException.Validation("org_scope_invalid", "A branch scope requires a clinic.");
        }

        Role = role;
        ClinicId = clinicId;
        BranchId = branchId;
        IsActive = true;
        UpdatedAtUtc = now;
    }

    public void TransferBranch(Guid clinicId, Guid? branchId, DateTimeOffset now)
    {
        ClinicId = clinicId;
        BranchId = branchId;
        UpdatedAtUtc = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAtUtc = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAtUtc = now;
    }

    public bool CoversClinic(Guid clinicId) => ClinicId is null || ClinicId == clinicId;

    public bool CoversBranch(Guid branchId) => BranchId is null || BranchId == branchId;
}

public sealed class OrganizationSetting
{
    private OrganizationSetting()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrganizationSetting Upsert(Guid organizationId, string key, string value, DateTimeOffset now)
    {
        var k = key.Trim();
        var v = value.Trim();
        if (k.Length is < 2 or > 120 || v.Length > 2000)
        {
            throw DomainException.Validation("org_setting_invalid", "Enter a valid organization setting.");
        }

        return new OrganizationSetting
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Key = k,
            Value = v,
            UpdatedAtUtc = now
        };
    }

    public void SetValue(string value, DateTimeOffset now)
    {
        Value = value.Trim();
        UpdatedAtUtc = now;
    }
}

public sealed class BranchSettingOverride
{
    private BranchSettingOverride()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BranchId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static BranchSettingOverride Upsert(Guid organizationId, Guid branchId, string key, string value, DateTimeOffset now)
    {
        var k = key.Trim();
        var v = value.Trim();
        if (k.Length is < 2 or > 120 || v.Length > 2000)
        {
            throw DomainException.Validation("branch_setting_invalid", "Enter a valid branch setting override.");
        }

        return new BranchSettingOverride
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Key = k,
            Value = v,
            UpdatedAtUtc = now
        };
    }

    public void SetValue(string value, DateTimeOffset now)
    {
        Value = value.Trim();
        UpdatedAtUtc = now;
    }
}

public sealed class ConsultationResource
{
    private ConsultationResource()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ResourceKind Kind { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ConsultationResource Create(
        Guid organizationId,
        Guid clinicId,
        Guid branchId,
        string name,
        ResourceKind kind,
        DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 120)
        {
            throw DomainException.Validation("resource_name_invalid", "Enter a resource name between 2 and 120 characters.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw DomainException.Validation("resource_kind_invalid", "Choose a valid resource kind.");
        }

        return new ConsultationResource
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ClinicId = clinicId,
            BranchId = branchId,
            Name = text,
            Kind = kind,
            IsActive = true,
            CreatedAtUtc = now
        };
    }

    public void SetActive(bool active) => IsActive = active;
}

public sealed class ResourceAllocation
{
    private ResourceAllocation()
    {
    }

    public Guid Id { get; private set; }

    public Guid ResourceId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? DoctorId { get; private set; }

    public Guid? ScheduleId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ResourceAllocation Create(
        Guid resourceId,
        Guid organizationId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset now,
        Guid? doctorId = null,
        Guid? scheduleId = null)
    {
        if (endsAtUtc <= startsAtUtc)
        {
            throw DomainException.Validation("resource_window_invalid", "Resource end must be after start.");
        }

        if (doctorId is null && scheduleId is null)
        {
            throw DomainException.Validation("resource_allocation_invalid", "Assign a doctor or schedule to the resource.");
        }

        return new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = resourceId,
            OrganizationId = organizationId,
            DoctorId = doctorId,
            ScheduleId = scheduleId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            CreatedAtUtc = now
        };
    }

    public bool Overlaps(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc) =>
        StartsAtUtc < endsAtUtc && EndsAtUtc > startsAtUtc;
}

public sealed class OrganizationSubscription
{
    private OrganizationSubscription()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? PlanId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public OrganizationSubscriptionStatus Status { get; private set; }

    public int SeatLimit { get; private set; }

    public string? BillingEmail { get; private set; }

    public DateTimeOffset? CurrentPeriodStartUtc { get; private set; }

    public DateTimeOffset? CurrentPeriodEndUtc { get; private set; }

    public DateTimeOffset? TrialEndsUtc { get; private set; }

    public DateTimeOffset? GracePeriodEndsUtc { get; private set; }

    public int BillingInterval { get; private set; } = 1;

    public bool CancelAtPeriodEnd { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? PendingPlanCode { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static OrganizationSubscription Create(
        Guid organizationId,
        string planCode,
        int seatLimit,
        string? billingEmail,
        DateTimeOffset now,
        Guid? planId = null,
        int trialDays = 30,
        int billingInterval = 1)
    {
        var plan = planCode.Trim().ToUpperInvariant();
        if (plan.Length is < 2 or > 40)
        {
            throw DomainException.Validation("subscription_plan_invalid", "Enter a valid subscription plan code.");
        }

        if (seatLimit is < 1 or > 100000)
        {
            throw DomainException.Validation("subscription_seats_invalid", "Seat limit must be between 1 and 100000.");
        }

        var trial = Math.Clamp(trialDays, 0, 365);
        return new OrganizationSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlanId = planId,
            PlanCode = plan,
            Status = OrganizationSubscriptionStatus.Trial,
            SeatLimit = seatLimit,
            BillingEmail = string.IsNullOrWhiteSpace(billingEmail) ? null : billingEmail.Trim(),
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = now.AddDays(Math.Max(trial, 1)),
            TrialEndsUtc = trial > 0 ? now.AddDays(trial) : null,
            BillingInterval = billingInterval is 1 or 2 ? billingInterval : 1,
            UpdatedAtUtc = now
        };
    }

    public void Update(string planCode, OrganizationSubscriptionStatus status, int seatLimit, string? billingEmail, DateTimeOffset? periodEnd, DateTimeOffset now)
    {
        var plan = planCode.Trim().ToUpperInvariant();
        if (plan.Length is < 2 or > 40)
        {
            throw DomainException.Validation("subscription_plan_invalid", "Enter a valid subscription plan code.");
        }

        if (!Enum.IsDefined(status))
        {
            throw DomainException.Validation("subscription_status_invalid", "Choose a valid subscription status.");
        }

        if (seatLimit is < 1 or > 100000)
        {
            throw DomainException.Validation("subscription_seats_invalid", "Seat limit must be between 1 and 100000.");
        }

        PlanCode = plan;
        Status = status;
        SeatLimit = seatLimit;
        BillingEmail = string.IsNullOrWhiteSpace(billingEmail) ? null : billingEmail.Trim();
        CurrentPeriodEndUtc = periodEnd;
        UpdatedAtUtc = now;
    }

    public void AssignPlan(Guid planId, string planCode, int seatLimit, int billingInterval, DateTimeOffset now)
    {
        PlanId = planId;
        PlanCode = planCode.Trim().ToUpperInvariant();
        SeatLimit = seatLimit;
        BillingInterval = billingInterval is 1 or 2 ? billingInterval : 1;
        PendingPlanCode = null;
        UpdatedAtUtc = now;
    }

    public void MarkActivePeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now)
    {
        Status = OrganizationSubscriptionStatus.Active;
        CurrentPeriodStartUtc = periodStart;
        CurrentPeriodEndUtc = periodEnd;
        GracePeriodEndsUtc = null;
        TrialEndsUtc = null;
        UpdatedAtUtc = now;
    }

    public void EnterPastDue(DateTimeOffset graceEnds, DateTimeOffset now)
    {
        Status = OrganizationSubscriptionStatus.PastDue;
        GracePeriodEndsUtc = graceEnds;
        UpdatedAtUtc = now;
    }

    public void RequestCancelAtPeriodEnd(DateTimeOffset now)
    {
        CancelAtPeriodEnd = true;
        CancelledAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void ClearCancellation(DateTimeOffset now)
    {
        CancelAtPeriodEnd = false;
        CancelledAtUtc = null;
        UpdatedAtUtc = now;
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        Status = OrganizationSubscriptionStatus.Cancelled;
        CancelAtPeriodEnd = false;
        CancelledAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = OrganizationSubscriptionStatus.Suspended;
        UpdatedAtUtc = now;
    }

    public void SchedulePlanChange(string pendingPlanCode, DateTimeOffset now)
    {
        PendingPlanCode = pendingPlanCode.Trim().ToUpperInvariant();
        UpdatedAtUtc = now;
    }

    public bool IsOperationallyActive(DateTimeOffset now) =>
        Status is OrganizationSubscriptionStatus.Trial or OrganizationSubscriptionStatus.Active
        || (Status == OrganizationSubscriptionStatus.PastDue && GracePeriodEndsUtc is DateTimeOffset grace && grace >= now);
}

public static class OrganizationSettingKeys
{
    public const string DefaultAppointmentChangeHours = "appointments.patient_change_hours";
    public const string DefaultQueueEarlyCheckInMinutes = "queue.early_check_in_minutes";
    public const string DefaultCurrency = "billing.currency";
}
