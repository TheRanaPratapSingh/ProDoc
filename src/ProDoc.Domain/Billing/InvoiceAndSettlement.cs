using ProDoc.Domain.Common;

namespace ProDoc.Domain.Billing;

public sealed class CommissionSnapshot
{
    private CommissionSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid? CommissionRuleId { get; private set; }

    public string RuleNameSnapshot { get; private set; } = string.Empty;

    public CommissionBasis BasisSnapshot { get; private set; }

    public decimal RateSnapshot { get; private set; }

    public decimal ConsultationFee { get; private set; }

    public decimal CommissionAmount { get; private set; }

    public decimal ClinicPayableAmount { get; private set; }

    public decimal GatewayFeeSnapshot { get; private set; }

    public string Currency { get; private set; } = "INR";

    public bool IsWalkInExcluded { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public Guid? SettlementBatchId { get; private set; }

    public static CommissionSnapshot Capture(
        Guid appointmentId,
        Guid clinicId,
        Guid doctorId,
        CommissionRule? rule,
        decimal consultationFee,
        decimal gatewayFee,
        bool walkInExcluded,
        DateTimeOffset now)
    {
        var commission = walkInExcluded || rule is null ? 0m : rule.Calculate(consultationFee);
        var clinicPayable = decimal.Round(consultationFee - commission - gatewayFee, 2, MidpointRounding.AwayFromZero);
        if (clinicPayable < 0)
        {
            clinicPayable = 0;
        }

        return new CommissionSnapshot
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            ClinicId = clinicId,
            DoctorId = doctorId,
            CommissionRuleId = rule?.Id,
            RuleNameSnapshot = rule?.Name ?? "No commission",
            BasisSnapshot = rule?.Basis ?? CommissionBasis.Percentage,
            RateSnapshot = rule?.Rate ?? 0m,
            ConsultationFee = decimal.Round(consultationFee, 2),
            CommissionAmount = commission,
            ClinicPayableAmount = clinicPayable,
            GatewayFeeSnapshot = decimal.Round(gatewayFee, 2),
            IsWalkInExcluded = walkInExcluded,
            CapturedAtUtc = now
        };
    }

    public void AssignSettlement(Guid batchId) => SettlementBatchId = batchId;
}

public sealed class Invoice
{
    private Invoice()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public InvoiceKind Kind { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? AccountUserId { get; private set; }

    public Guid? SettlementBatchId { get; private set; }

    public string BillToName { get; private set; } = string.Empty;

    public string? BillToDetail { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Subtotal { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal Total { get; private set; }

    public decimal AmountPaid { get; private set; }

    public decimal Outstanding { get; private set; }

    public string Currency { get; private set; } = "INR";

    public string? TaxCodeSnapshot { get; private set; }

    public decimal TaxRateSnapshot { get; private set; }

    public string Status { get; private set; } = "Issued";

    public string? PdfStorageKey { get; private set; }

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public static Invoice Issue(
        string number,
        InvoiceKind kind,
        string billToName,
        string? billToDetail,
        string description,
        decimal subtotal,
        decimal taxAmount,
        decimal amountPaid,
        string currency,
        string? taxCode,
        decimal taxRate,
        DateTimeOffset now,
        Guid? appointmentId = null,
        Guid? paymentId = null,
        Guid? clinicId = null,
        Guid? accountUserId = null,
        Guid? settlementBatchId = null)
    {
        var total = decimal.Round(subtotal + taxAmount, 2);
        var outstanding = decimal.Round(total - amountPaid, 2);
        return new Invoice
        {
            Id = Guid.NewGuid(),
            Number = number,
            Kind = kind,
            AppointmentId = appointmentId,
            PaymentId = paymentId,
            ClinicId = clinicId,
            AccountUserId = accountUserId,
            SettlementBatchId = settlementBatchId,
            BillToName = billToName.Trim(),
            BillToDetail = string.IsNullOrWhiteSpace(billToDetail) ? null : billToDetail.Trim(),
            Description = description.Trim(),
            Subtotal = decimal.Round(subtotal, 2),
            TaxAmount = decimal.Round(taxAmount, 2),
            Total = total,
            AmountPaid = decimal.Round(amountPaid, 2),
            Outstanding = outstanding < 0 ? 0 : outstanding,
            Currency = currency,
            TaxCodeSnapshot = taxCode,
            TaxRateSnapshot = taxRate,
            Status = outstanding <= 0 ? "Paid" : "Issued",
            IssuedAtUtc = now
        };
    }

    public void AttachPdf(string storageKey) => PdfStorageKey = storageKey;

    public void RecordPayment(decimal amount, DateTimeOffset now)
    {
        _ = now;
        AmountPaid = decimal.Round(AmountPaid + amount, 2);
        Outstanding = decimal.Round(Total - AmountPaid, 2);
        if (Outstanding < 0)
        {
            Outstanding = 0;
        }

        if (Outstanding <= 0)
        {
            Status = "Paid";
        }
    }
}

public sealed class SettlementBatch
{
    private readonly List<SettlementLine> _lines = [];

    private SettlementBatch()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid ClinicId { get; private set; }

    public SettlementStatus Status { get; private set; }

    public DateOnly PeriodFrom { get; private set; }

    public DateOnly PeriodTo { get; private set; }

    public decimal GrossConsultationFees { get; private set; }

    public decimal TotalCommission { get; private set; }

    public decimal TotalGatewayFees { get; private set; }

    public decimal NetPayable { get; private set; }

    public string Currency { get; private set; } = "INR";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? FinalizedAtUtc { get; private set; }

    public IReadOnlyCollection<SettlementLine> Lines => _lines;

    public static SettlementBatch Open(
        string number,
        Guid clinicId,
        DateOnly periodFrom,
        DateOnly periodTo,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Number = number,
            ClinicId = clinicId,
            Status = SettlementStatus.Open,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            CreatedAtUtc = now
        };

    public void AddLine(CommissionSnapshot snapshot)
    {
        if (Status != SettlementStatus.Open)
        {
            throw DomainException.Conflict("settlement_locked", "This settlement is already finalized.");
        }

        if (snapshot.ClinicId != ClinicId)
        {
            throw DomainException.Validation("settlement_clinic_mismatch", "Commission belongs to another clinic.");
        }

        _lines.Add(SettlementLine.Create(Id, snapshot.Id, snapshot.AppointmentId, snapshot.ConsultationFee, snapshot.CommissionAmount, snapshot.GatewayFeeSnapshot, snapshot.ClinicPayableAmount));
        GrossConsultationFees = decimal.Round(_lines.Sum(item => item.ConsultationFee), 2);
        TotalCommission = decimal.Round(_lines.Sum(item => item.CommissionAmount), 2);
        TotalGatewayFees = decimal.Round(_lines.Sum(item => item.GatewayFee), 2);
        NetPayable = decimal.Round(_lines.Sum(item => item.ClinicPayable), 2);
        snapshot.AssignSettlement(Id);
    }

    public void Finalize(DateTimeOffset now)
    {
        if (Status != SettlementStatus.Open)
        {
            throw DomainException.Conflict("settlement_locked", "This settlement is already finalized.");
        }

        if (_lines.Count == 0)
        {
            throw DomainException.Validation("settlement_empty", "Add at least one commission line before finalizing.");
        }

        Status = SettlementStatus.Finalized;
        FinalizedAtUtc = now;
    }
}

public sealed class SettlementLine
{
    private SettlementLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid SettlementBatchId { get; private set; }

    public Guid CommissionSnapshotId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public decimal ConsultationFee { get; private set; }

    public decimal CommissionAmount { get; private set; }

    public decimal GatewayFee { get; private set; }

    public decimal ClinicPayable { get; private set; }

    public static SettlementLine Create(
        Guid batchId,
        Guid snapshotId,
        Guid appointmentId,
        decimal consultationFee,
        decimal commission,
        decimal gatewayFee,
        decimal clinicPayable) =>
        new()
        {
            Id = Guid.NewGuid(),
            SettlementBatchId = batchId,
            CommissionSnapshotId = snapshotId,
            AppointmentId = appointmentId,
            ConsultationFee = consultationFee,
            CommissionAmount = commission,
            GatewayFee = gatewayFee,
            ClinicPayable = clinicPayable
        };
}
