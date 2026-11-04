using ProDoc.Domain.Common;

namespace ProDoc.Domain.Billing;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid AppointmentId { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid DoctorId { get; private set; }

    public PaymentChannel Channel { get; private set; }

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public decimal ConsultationFeeSnapshot { get; private set; }

    public decimal GatewayFeeEstimated { get; private set; }

    public string Currency { get; private set; } = "INR";

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderOrderId { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? CheckoutPayload { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public static Payment CreateOnline(
        string number,
        Guid appointmentId,
        Guid accountUserId,
        Guid clinicId,
        Guid doctorId,
        decimal amount,
        decimal consultationFee,
        decimal gatewayFeeEstimated,
        string currency,
        string provider,
        string idempotencyKey,
        DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw DomainException.Validation("payment_amount_invalid", "Payment amount must be greater than zero.");
        }

        return new Payment
        {
            Id = Guid.NewGuid(),
            Number = number,
            AppointmentId = appointmentId,
            AccountUserId = accountUserId,
            ClinicId = clinicId,
            DoctorId = doctorId,
            Channel = PaymentChannel.OnlineGateway,
            Status = PaymentStatus.Created,
            Amount = decimal.Round(amount, 2),
            ConsultationFeeSnapshot = decimal.Round(consultationFee, 2),
            GatewayFeeEstimated = decimal.Round(gatewayFeeEstimated, 2),
            Currency = currency,
            Provider = provider,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static Payment RecordClinicCollection(
        string number,
        Guid appointmentId,
        Guid accountUserId,
        Guid clinicId,
        Guid doctorId,
        decimal amount,
        decimal consultationFee,
        string currency,
        Guid collectedByUserId,
        DateTimeOffset now)
    {
        _ = collectedByUserId;
        return new Payment
        {
            Id = Guid.NewGuid(),
            Number = number,
            AppointmentId = appointmentId,
            AccountUserId = accountUserId,
            ClinicId = clinicId,
            DoctorId = doctorId,
            Channel = PaymentChannel.PayAtClinic,
            Status = PaymentStatus.Paid,
            Amount = decimal.Round(amount, 2),
            ConsultationFeeSnapshot = decimal.Round(consultationFee, 2),
            GatewayFeeEstimated = 0m,
            Currency = currency,
            Provider = "clinic-desk",
            IdempotencyKey = $"clinic:{appointmentId:N}",
            PaidAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void AttachProviderOrder(string orderId, string? checkoutPayload, DateTimeOffset now)
    {
        if (Status is not (PaymentStatus.Created or PaymentStatus.Pending))
        {
            throw DomainException.Conflict("payment_not_pending", "This payment can no longer be started.");
        }

        ProviderOrderId = orderId;
        CheckoutPayload = checkoutPayload;
        Status = PaymentStatus.Pending;
        UpdatedAtUtc = now;
    }

    public void MarkPaid(string providerPaymentId, DateTimeOffset now)
    {
        if (Status == PaymentStatus.Paid)
        {
            return;
        }

        if (Status is not (PaymentStatus.Created or PaymentStatus.Pending or PaymentStatus.Failed))
        {
            throw DomainException.Conflict("payment_not_payable", "This payment cannot be marked paid.");
        }

        ProviderPaymentId = providerPaymentId;
        Status = PaymentStatus.Paid;
        PaidAtUtc = now;
        FailureReason = null;
        UpdatedAtUtc = now;
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        if (Status == PaymentStatus.Paid)
        {
            throw DomainException.Conflict("payment_already_paid", "A paid payment cannot fail.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed." : reason.Trim()[..Math.Min(reason.Trim().Length, 240)];
        UpdatedAtUtc = now;
    }

    public void MarkRefunded(bool partial, DateTimeOffset now)
    {
        if (Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            throw DomainException.Conflict("payment_not_refundable", "Only a paid payment can be refunded.");
        }

        Status = partial ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
        UpdatedAtUtc = now;
    }
}

public sealed class PaymentWebhookEvent
{
    private PaymentWebhookEvent()
    {
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string EventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public Guid? PaymentId { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public static PaymentWebhookEvent Record(string provider, string eventId, string eventType, Guid? paymentId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventId = eventId,
            EventType = eventType,
            PaymentId = paymentId,
            ReceivedAtUtc = now
        };
}

public sealed class Refund
{
    private Refund()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid PaymentId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "INR";

    public RefundStatus Status { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string? ProviderRefundId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Refund Request(
        string number,
        Guid paymentId,
        Guid appointmentId,
        decimal amount,
        string currency,
        string reason,
        Guid requestedByUserId,
        DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw DomainException.Validation("refund_amount_invalid", "Refund amount must be greater than zero.");
        }

        var text = reason.Trim();
        if (text.Length is < 4 or > 240)
        {
            throw DomainException.Validation("refund_reason_invalid", "Enter a refund reason from 4 to 240 characters.");
        }

        return new Refund
        {
            Id = Guid.NewGuid(),
            Number = number,
            PaymentId = paymentId,
            AppointmentId = appointmentId,
            Amount = decimal.Round(amount, 2),
            Currency = currency,
            Status = RefundStatus.Requested,
            Reason = text,
            RequestedByUserId = requestedByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkProcessing(string? providerRefundId, DateTimeOffset now)
    {
        Status = RefundStatus.Processing;
        ProviderRefundId = providerRefundId;
        UpdatedAtUtc = now;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = RefundStatus.Completed;
        CompletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        Status = RefundStatus.Failed;
        UpdatedAtUtc = now;
    }
}
