namespace ProDoc.Application.Billing;

public sealed record ClinicPaymentSettingsDetails(
    Guid ClinicId,
    string Policy,
    decimal AdvancePercent,
    bool ChargeCommissionOnWalkIns);

public sealed record PaymentDetails(
    Guid Id,
    string Number,
    Guid AppointmentId,
    string Channel,
    string Status,
    decimal Amount,
    decimal ConsultationFeeSnapshot,
    decimal GatewayFeeEstimated,
    string Currency,
    string Provider,
    string? ProviderOrderId,
    string? CheckoutPayload,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc);

public sealed record RefundDetails(
    Guid Id,
    string Number,
    Guid PaymentId,
    Guid AppointmentId,
    decimal Amount,
    string Status,
    string Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record CommissionRuleDetails(
    Guid Id,
    string Name,
    string Basis,
    decimal Rate,
    decimal? MinimumAmount,
    decimal? MaximumAmount,
    Guid? ClinicId,
    Guid? DoctorId,
    bool AppliesToPlatformBookingsOnly,
    bool IsActive,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int Priority);

public sealed record CommissionSnapshotDetails(
    Guid Id,
    Guid AppointmentId,
    Guid ClinicId,
    Guid DoctorId,
    string RuleName,
    decimal ConsultationFee,
    decimal CommissionAmount,
    decimal ClinicPayableAmount,
    decimal GatewayFee,
    bool IsWalkInExcluded,
    DateTimeOffset CapturedAtUtc,
    Guid? SettlementBatchId);

public sealed record InvoiceDetails(
    Guid Id,
    string Number,
    string Kind,
    Guid? AppointmentId,
    Guid? PaymentId,
    Guid? ClinicId,
    string BillToName,
    string Description,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    decimal AmountPaid,
    decimal Outstanding,
    string Status,
    bool HasPdf,
    DateTimeOffset IssuedAtUtc);

public sealed record SettlementDetails(
    Guid Id,
    string Number,
    Guid ClinicId,
    string Status,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal GrossConsultationFees,
    decimal TotalCommission,
    decimal TotalGatewayFees,
    decimal NetPayable,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinalizedAtUtc,
    int LineCount);

public sealed record ClinicFinanceDashboard(
    Guid ClinicId,
    decimal GrossEligibleFees,
    decimal OnlineCollected,
    decimal PayAtClinicCollected,
    decimal CommissionDeducted,
    decimal Refunds,
    decimal PendingSettlement,
    decimal NetPayable,
    IReadOnlyList<CommissionSnapshotDetails> RecentCommissions,
    IReadOnlyList<PaymentDetails> RecentPayments);

public sealed record AdminFinanceDashboard(
    decimal GrossPlatformBookingValue,
    decimal OnlineCollected,
    decimal CommissionEarned,
    decimal RefundsIssued,
    decimal PendingClinicPayables,
    int CompletedEligibleVisits,
    IReadOnlyList<SettlementDetails> RecentSettlements);

public sealed record StoredInvoicePdf(Stream Content, string FileName, string ContentType);
