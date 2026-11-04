namespace ProDoc.Domain.Billing;

public enum ClinicPaymentPolicy
{
    PayAtClinicOnly = 1,
    OnlineOptional = 2,
    OnlineRequired = 3,
    PartialAdvance = 4
}

public enum PaymentStatus
{
    Created = 1,
    Pending = 2,
    Paid = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6,
    PartiallyRefunded = 7
}

public enum PaymentChannel
{
    OnlineGateway = 1,
    PayAtClinic = 2
}

public enum RefundStatus
{
    Requested = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}

public enum CommissionBasis
{
    Percentage = 1,
    Fixed = 2
}

public enum InvoiceKind
{
    PatientReceipt = 1,
    ClinicCommissionInvoice = 2,
    SettlementStatement = 3,
    RefundCreditNote = 4,
    SaaSSubscription = 5
}

public enum SettlementStatus
{
    Open = 1,
    Finalized = 2,
    PaidOut = 3
}

public static class AppointmentPaymentMethods
{
    public const string PayAtClinic = "PayAtClinic";

    public const string PayOnline = "PayOnline";

    public const string PartialOnline = "PartialOnline";
}
