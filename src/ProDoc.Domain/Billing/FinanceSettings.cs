using ProDoc.Domain.Common;

namespace ProDoc.Domain.Billing;

public sealed class ClinicPaymentSettings
{
    private ClinicPaymentSettings()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public ClinicPaymentPolicy Policy { get; private set; } = ClinicPaymentPolicy.OnlineOptional;

    public decimal AdvancePercent { get; private set; }

    public bool ChargeCommissionOnWalkIns { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ClinicPaymentSettings CreateDefault(Guid clinicId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            UpdatedAtUtc = now
        };

    public void Update(ClinicPaymentPolicy policy, decimal advancePercent, bool chargeCommissionOnWalkIns, DateTimeOffset now)
    {
        if (policy == ClinicPaymentPolicy.PartialAdvance && (advancePercent is < 1 or > 99))
        {
            throw DomainException.Validation("advance_percent_invalid", "Advance percent must be between 1 and 99.");
        }

        Policy = policy;
        AdvancePercent = policy == ClinicPaymentPolicy.PartialAdvance ? decimal.Round(advancePercent, 2) : 0m;
        ChargeCommissionOnWalkIns = chargeCommissionOnWalkIns;
        UpdatedAtUtc = now;
    }
}

public sealed class TaxRule
{
    private TaxRule()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal RatePercent { get; private set; }

    public bool IsInclusive { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TaxRule Create(
        string code,
        string name,
        decimal ratePercent,
        bool isInclusive,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        DateTimeOffset now)
    {
        if (ratePercent is < 0 or > 100)
        {
            throw DomainException.Validation("tax_rate_invalid", "Tax rate must be between 0 and 100.");
        }

        return new TaxRule
        {
            Id = Guid.NewGuid(),
            Code = Required(code, 2, 32),
            Name = Required(name, 2, 80),
            RatePercent = decimal.Round(ratePercent, 2),
            IsInclusive = isInclusive,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedAtUtc = now
        };
    }

    private static string Required(string value, int min, int max)
    {
        var text = value.Trim();
        if (text.Length < min || text.Length > max)
        {
            throw DomainException.Validation("tax_rule_invalid", "Enter a valid tax rule code and name.");
        }

        return text;
    }
}

public sealed class CommissionRule
{
    private CommissionRule()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CommissionBasis Basis { get; private set; }

    public decimal Rate { get; private set; }

    public decimal? MinimumAmount { get; private set; }

    public decimal? MaximumAmount { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? DoctorId { get; private set; }

    public bool AppliesToPlatformBookingsOnly { get; private set; } = true;

    public bool IsActive { get; private set; } = true;

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public int Priority { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CommissionRule Create(
        string name,
        CommissionBasis basis,
        decimal rate,
        decimal? minimumAmount,
        decimal? maximumAmount,
        Guid? clinicId,
        Guid? doctorId,
        bool platformBookingsOnly,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        int priority,
        DateTimeOffset now)
    {
        if (basis == CommissionBasis.Percentage && rate is < 0 or > 100)
        {
            throw DomainException.Validation("commission_rate_invalid", "Percentage commission must be between 0 and 100.");
        }

        if (basis == CommissionBasis.Fixed && rate < 0)
        {
            throw DomainException.Validation("commission_rate_invalid", "Fixed commission cannot be negative.");
        }

        return new CommissionRule
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Basis = basis,
            Rate = decimal.Round(rate, 2),
            MinimumAmount = minimumAmount,
            MaximumAmount = maximumAmount,
            ClinicId = clinicId,
            DoctorId = doctorId,
            AppliesToPlatformBookingsOnly = platformBookingsOnly,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Priority = priority,
            CreatedAtUtc = now
        };
    }

    public decimal Calculate(decimal consultationFee)
    {
        var amount = Basis == CommissionBasis.Percentage
            ? decimal.Round(consultationFee * Rate / 100m, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(Rate, 2, MidpointRounding.AwayFromZero);
        if (MinimumAmount is not null && amount < MinimumAmount)
        {
            amount = MinimumAmount.Value;
        }

        if (MaximumAmount is not null && amount > MaximumAmount)
        {
            amount = MaximumAmount.Value;
        }

        return amount < 0 ? 0 : amount;
    }

    public void Deactivate() => IsActive = false;
}
