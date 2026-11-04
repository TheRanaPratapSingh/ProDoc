using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Enterprise;
using ProDoc.Application.Growth;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Billing;

public sealed class FinanceService(
    IFinanceStore store,
    IPaymentGateway gateway,
    IInvoicePdfGenerator invoicePdfs,
    IDocumentStorage documents,
    IOptions<PaymentGatewayOptions> options,
    IClock clock,
    GrowthService growth,
    EnterpriseService enterprise,
    IAutomationEventBus automation,
    ILogger<FinanceService> logger)
{
    public async Task<ClinicPaymentSettingsDetails> GetClinicPaymentSettingsAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        await EnsureClinicOwnerAsync(userId, clinicId, cancellationToken);
        var settings = await EnsureSettingsAsync(clinicId, cancellationToken);
        return MapSettings(settings);
    }

    public async Task<ClinicPaymentSettingsDetails> UpdateClinicPaymentSettingsAsync(
        Guid userId,
        Guid clinicId,
        string policy,
        decimal advancePercent,
        bool chargeCommissionOnWalkIns,
        CancellationToken cancellationToken)
    {
        await EnsureClinicOwnerAsync(userId, clinicId, cancellationToken);
        if (!Enum.TryParse<ClinicPaymentPolicy>(policy, true, out var parsed))
        {
            throw DomainException.Validation("payment_policy_invalid", "Choose a valid clinic payment policy.");
        }

        var settings = await EnsureSettingsAsync(clinicId, cancellationToken);
        settings.Update(parsed, advancePercent, chargeCommissionOnWalkIns, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<PaymentDetails> CreateAppointmentPaymentAsync(
        Guid userId,
        Guid appointmentId,
        string? paymentMethod,
        string idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken,
        string? couponCode = null,
        bool useRewardCredit = false)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 80)
        {
            throw DomainException.Validation("idempotency_invalid", "Provide an idempotency key up to 80 characters.");
        }

        var existing = await store.FindPaymentByIdempotencyAsync(idempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            return MapPayment(existing);
        }

        var appointment = await store.FindAppointmentAsync(appointmentId, true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected or AppointmentStatus.Expired)
        {
            throw DomainException.Conflict("appointment_not_payable", "This appointment cannot accept payment.");
        }

        var paid = await store.FindPaidPaymentForAppointmentAsync(appointmentId, cancellationToken);
        if (paid is not null || appointment.PaymentStatus == "Paid")
        {
            throw DomainException.Conflict("payment_already_collected", "This appointment is already paid.");
        }

        var settings = await EnsureSettingsAsync(appointment.ClinicId, cancellationToken);
        var method = string.IsNullOrWhiteSpace(paymentMethod) ? AppointmentPaymentMethods.PayOnline : paymentMethod.Trim();
        EnsureMethodAllowed(settings, method);
        appointment.SetPaymentMethod(method, clock.UtcNow);

        var baseAmount = method == AppointmentPaymentMethods.PartialOnline
            ? decimal.Round(appointment.ConsultationFee * settings.AdvancePercent / 100m, 2, MidpointRounding.AwayFromZero)
            : appointment.ConsultationFee;
        var (discount, redemption) = await growth.ApplyCouponForPaymentAsync(userId, appointment, couponCode, idempotencyKey.Trim(), cancellationToken);
        var afterCoupon = Math.Max(0m, baseAmount - discount);
        var (creditUsed, _) = await growth.ApplyRewardCreditForPaymentAsync(
            userId,
            appointment,
            afterCoupon,
            useRewardCredit,
            idempotencyKey.Trim(),
            cancellationToken);
        var amount = Math.Max(0m, afterCoupon - creditUsed);
        if (amount <= 0 && discount + creditUsed <= 0)
        {
            throw DomainException.Validation("payment_amount_invalid", "Payment amount must be greater than zero.");
        }

        if (amount <= 0)
        {
            amount = 0.01m;
        }

        var gatewayFee = decimal.Round(amount * options.Value.EstimatedGatewayFeePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var number = await store.NextNumberAsync("PAY", cancellationToken);
        var payment = Payment.CreateOnline(
            number,
            appointment.Id,
            appointment.AccountUserId,
            appointment.ClinicId,
            appointment.DoctorId,
            amount,
            appointment.ConsultationFee,
            gatewayFee,
            appointment.Currency,
            gateway.ProviderName,
            idempotencyKey.Trim(),
            clock.UtcNow);
        var order = await gateway.CreateOrderAsync(
            new GatewayOrderRequest(payment.Number, amount, appointment.Currency, appointment.Reference ?? appointment.Id.ToString("N"), payment.IdempotencyKey),
            cancellationToken);
        payment.AttachProviderOrder(order.OrderId, order.CheckoutPayloadJson, clock.UtcNow);
        appointment.AttachPayment(payment.Id, nameof(PaymentStatus.Pending), 0m, clock.UtcNow);
        store.AddPayment(payment);
        redemption?.AttachPayment(payment.Id);
        await store.AddAuditAsync(Audit(userId, AuditActions.PaymentCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPayment(payment);
    }

    public async Task<PaymentDetails> VerifyPaymentAsync(
        Guid userId,
        Guid paymentId,
        string? providerPaymentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var payment = await store.FindPaymentAsync(paymentId, true, cancellationToken)
            ?? throw DomainException.NotFound("payment_not_found", "That payment was not found.");
        if (payment.AccountUserId != userId)
        {
            throw DomainException.NotFound("payment_not_found", "That payment was not found.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return MapPayment(payment);
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderOrderId))
        {
            throw DomainException.Conflict("payment_not_started", "Start checkout before verifying payment.");
        }

        var verification = await gateway.VerifyPaymentAsync(payment.ProviderOrderId, providerPaymentId, cancellationToken);
        if (!verification.Paid)
        {
            payment.MarkFailed(verification.FailureReason ?? "Payment was not completed.", clock.UtcNow);
            var appointmentFailed = await store.FindAppointmentAsync(payment.AppointmentId, true, cancellationToken);
            appointmentFailed?.AttachPayment(payment.Id, nameof(PaymentStatus.Failed), 0m, clock.UtcNow);
            await store.AddAuditAsync(Audit(userId, AuditActions.PaymentFailed, metadata), cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            await PublishPaymentAsync(AutomationTriggerTypes.PaymentFailed, payment, appointmentFailed, cancellationToken);
            throw DomainException.Conflict("payment_not_completed", verification.FailureReason ?? "Payment was not completed.");
        }

        await MarkPaidInternalAsync(payment, verification.ProviderPaymentId ?? providerPaymentId ?? payment.ProviderOrderId, userId, metadata, cancellationToken);
        return MapPayment(payment);
    }

    public async Task HandleWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var parsed = gateway.ParseWebhook(rawBody, headers);
        if (!parsed.SignatureValid)
        {
            throw DomainException.Unauthorized("webhook_invalid", "Webhook signature was rejected.");
        }

        if (await store.WebhookExistsAsync(gateway.ProviderName, parsed.EventId, cancellationToken))
        {
            return;
        }

        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(parsed.ProviderOrderId))
        {
            payment = await store.FindPaymentByProviderOrderAsync(parsed.ProviderOrderId, cancellationToken);
        }

        store.AddWebhook(PaymentWebhookEvent.Record(gateway.ProviderName, parsed.EventId, parsed.EventType, payment?.Id, clock.UtcNow));
        if (payment is not null && parsed.PaymentCaptured && payment.Status != PaymentStatus.Paid)
        {
            var tracked = await store.FindPaymentAsync(payment.Id, true, cancellationToken)
                ?? throw DomainException.NotFound("payment_not_found", "That payment was not found.");
            await MarkPaidInternalAsync(
                tracked,
                parsed.ProviderPaymentId ?? tracked.ProviderOrderId ?? parsed.EventId,
                tracked.AccountUserId,
                new RequestMetadata(null, "webhook"),
                cancellationToken);
            return;
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentDetails> RecordClinicCollectionAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await store.FindAppointmentAsync(appointmentId, true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        await EnsureClinicOwnerOrDoctorAsync(userId, appointment, cancellationToken);
        var paid = await store.FindPaidPaymentForAppointmentAsync(appointmentId, cancellationToken);
        if (paid is not null)
        {
            throw DomainException.Conflict("payment_already_collected", "This appointment is already paid.");
        }

        var number = await store.NextNumberAsync("PAY", cancellationToken);
        var payment = Payment.RecordClinicCollection(
            number,
            appointment.Id,
            appointment.AccountUserId,
            appointment.ClinicId,
            appointment.DoctorId,
            appointment.ConsultationFee,
            appointment.ConsultationFee,
            appointment.Currency,
            userId,
            clock.UtcNow);
        appointment.SetPaymentMethod(AppointmentPaymentMethods.PayAtClinic, clock.UtcNow);
        appointment.AttachPayment(payment.Id, nameof(PaymentStatus.Paid), payment.Amount, clock.UtcNow);
        store.AddPayment(payment);
        await IssuePatientReceiptAsync(payment, appointment, cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.PaymentPaid, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await growth.OnPaymentPaidAsync(appointment.Id, payment.Id, cancellationToken);
        return MapPayment(payment);
    }

    public async Task<RefundDetails> RequestRefundAsync(
        Guid userId,
        Guid paymentId,
        decimal? amount,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var payment = await store.FindPaymentAsync(paymentId, true, cancellationToken)
            ?? throw DomainException.NotFound("payment_not_found", "That payment was not found.");
        var appointment = await store.FindAppointmentAsync(payment.AppointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        var isPatient = payment.AccountUserId == userId;
        if (!isPatient)
        {
            await EnsureClinicOwnerOrDoctorAsync(userId, appointment, cancellationToken);
        }

        return await ExecuteRefundAsync(userId, payment, amount, reason, metadata, cancellationToken);
    }

    /// <summary>
    /// Support-agent authorized refund path. Controllers must gate with admin.support.
    /// Still uses the standard refund aggregate and gateway — never edits ledgers directly.
    /// </summary>
    public async Task<RefundDetails> RequestSupportRefundAsync(
        Guid supportAgentUserId,
        Guid paymentId,
        decimal? amount,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var payment = await store.FindPaymentAsync(paymentId, true, cancellationToken)
            ?? throw DomainException.NotFound("payment_not_found", "That payment was not found.");
        return await ExecuteRefundAsync(supportAgentUserId, payment, amount, reason, metadata, cancellationToken);
    }

    private async Task<RefundDetails> ExecuteRefundAsync(
        Guid actorUserId,
        Payment payment,
        decimal? amount,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (payment.Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            throw DomainException.Conflict("payment_not_refundable", "Only a paid payment can be refunded.");
        }

        var refundAmount = amount ?? payment.Amount;
        if (refundAmount <= 0 || refundAmount > payment.Amount)
        {
            throw DomainException.Validation("refund_amount_invalid", "Refund amount must be within the paid amount.");
        }

        var number = await store.NextNumberAsync("RFD", cancellationToken);
        var refund = Refund.Request(number, payment.Id, payment.AppointmentId, refundAmount, payment.Currency, reason, actorUserId, clock.UtcNow);
        if (payment.Channel == PaymentChannel.OnlineGateway && !string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            var gatewayResult = await gateway.RefundAsync(payment.ProviderPaymentId, refundAmount, reason, cancellationToken);
            if (!gatewayResult.Accepted)
            {
                refund.MarkFailed(clock.UtcNow);
                store.AddRefund(refund);
                await store.SaveChangesAsync(cancellationToken);
                throw DomainException.Conflict("refund_failed", "The payment provider rejected the refund.");
            }

            refund.MarkProcessing(gatewayResult.ProviderRefundId, clock.UtcNow);
            refund.MarkCompleted(clock.UtcNow);
        }
        else
        {
            refund.MarkCompleted(clock.UtcNow);
        }

        payment.MarkRefunded(refundAmount < payment.Amount, clock.UtcNow);
        store.AddRefund(refund);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.RefundRequested, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapRefund(refund);
    }

    public async Task CaptureCommissionForCompletedAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var existing = await store.FindSnapshotByAppointmentAsync(appointmentId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken);
        if (appointment is null || appointment.Status != AppointmentStatus.Completed)
        {
            return;
        }

        var settings = await EnsureSettingsAsync(appointment.ClinicId, cancellationToken);
        var excludeWalkIn = appointment.IsWalkIn && !settings.ChargeCommissionOnWalkIns;
        var platformBooking = !appointment.IsWalkIn;
        var rule = excludeWalkIn
            ? null
            : await store.FindBestRuleAsync(
                appointment.ClinicId,
                appointment.DoctorId,
                DateOnly.FromDateTime(clock.UtcNow.UtcDateTime),
                platformBooking,
                cancellationToken);
        var paid = await store.FindPaidPaymentForAppointmentAsync(appointmentId, cancellationToken);
        var gatewayFee = paid?.GatewayFeeEstimated ?? 0m;
        var snapshot = CommissionSnapshot.Capture(
            appointment.Id,
            appointment.ClinicId,
            appointment.DoctorId,
            rule,
            appointment.ConsultationFee,
            gatewayFee,
            excludeWalkIn,
            clock.UtcNow);
        store.AddSnapshot(snapshot);
        await store.AddAuditAsync(AuditLog.Record(null, AuditActions.CommissionCaptured, clock.UtcNow, null, null), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommissionRuleDetails> CreateCommissionRuleAsync(
        Guid userId,
        string name,
        string basis,
        decimal rate,
        decimal? minimumAmount,
        decimal? maximumAmount,
        Guid? clinicId,
        Guid? doctorId,
        bool platformOnly,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        int priority,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = userId;
        if (!Enum.TryParse<CommissionBasis>(basis, true, out var parsed))
        {
            throw DomainException.Validation("commission_basis_invalid", "Choose percentage or fixed commission.");
        }

        var rule = CommissionRule.Create(
            name,
            parsed,
            rate,
            minimumAmount,
            maximumAmount,
            clinicId,
            doctorId,
            platformOnly,
            effectiveFrom,
            effectiveTo,
            priority,
            clock.UtcNow);
        store.AddRule(rule);
        await store.AddAuditAsync(Audit(userId, AuditActions.CommissionCaptured, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapRule(rule);
    }

    public async Task<IReadOnlyList<CommissionRuleDetails>> ListCommissionRulesAsync(CancellationToken cancellationToken)
    {
        var rows = await store.ListRulesAsync(cancellationToken);
        return rows.Select(MapRule).ToArray();
    }

    public async Task<SettlementDetails> CreateSettlementAsync(
        Guid userId,
        Guid clinicId,
        DateOnly periodFrom,
        DateOnly periodTo,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = userId;
        if (periodTo < periodFrom)
        {
            throw DomainException.Validation("settlement_period_invalid", "Choose a valid settlement period.");
        }

        var snapshots = await store.ListUnsettledSnapshotsAsync(clinicId, periodFrom, periodTo, cancellationToken);
        if (snapshots.Count == 0)
        {
            throw DomainException.Validation("settlement_empty", "No unsettled commissions were found for that period.");
        }

        var number = await store.NextNumberAsync("STL", cancellationToken);
        var batch = SettlementBatch.Open(number, clinicId, periodFrom, periodTo, clock.UtcNow);
        foreach (var snapshot in snapshots)
        {
            batch.AddLine(snapshot);
        }

        batch.Finalize(clock.UtcNow);
        store.AddSettlement(batch);
        var clinic = await store.FindClinicAsync(clinicId, cancellationToken);
        var invoiceNumber = await store.NextNumberAsync("INV", cancellationToken);
        var tax = await store.FindActiveTaxAsync(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken);
        var taxAmount = tax is null ? 0m : decimal.Round(batch.TotalCommission * tax.RatePercent / 100m, 2);
        var invoice = Invoice.Issue(
            invoiceNumber,
            InvoiceKind.ClinicCommissionInvoice,
            clinic?.Name ?? "Clinic",
            null,
            $"Commission for {periodFrom:yyyy-MM-dd} to {periodTo:yyyy-MM-dd}",
            batch.TotalCommission,
            taxAmount,
            0m,
            batch.Currency,
            tax?.Code,
            tax?.RatePercent ?? 0m,
            clock.UtcNow,
            clinicId: clinicId,
            settlementBatchId: batch.Id);
        var pdf = await invoicePdfs.GenerateAsync(invoice, cancellationToken);
        await using var stream = new MemoryStream(pdf);
        var stored = await documents.SaveAsync(stream, ".pdf", cancellationToken);
        invoice.AttachPdf(stored.StorageKey);
        store.AddInvoice(invoice);
        await store.AddAuditAsync(Audit(userId, AuditActions.SettlementFinalized, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapSettlement(batch);
    }

    public async Task<ClinicFinanceDashboard> ClinicDashboardAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        await EnsureClinicOwnerAsync(userId, clinicId, cancellationToken);
        return await store.BuildClinicDashboardAsync(clinicId, cancellationToken);
    }

    public Task<AdminFinanceDashboard> AdminDashboardAsync(CancellationToken cancellationToken) =>
        store.BuildAdminDashboardAsync(cancellationToken);

    public Task<IReadOnlyList<SettlementDetails>> ListSettlementsAsync(Guid? clinicId, CancellationToken cancellationToken) =>
        store.ListSettlementsAsync(clinicId, cancellationToken);

    public async Task<InvoiceDetails> GetInvoiceAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await store.FindInvoiceAsync(invoiceId, false, cancellationToken)
            ?? throw DomainException.NotFound("invoice_not_found", "That invoice was not found.");
        await EnsureCanReadInvoiceAsync(userId, invoice, cancellationToken);
        return MapInvoice(invoice);
    }

    public async Task<StoredInvoicePdf> DownloadInvoicePdfAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await store.FindInvoiceAsync(invoiceId, false, cancellationToken)
            ?? throw DomainException.NotFound("invoice_not_found", "That invoice was not found.");
        await EnsureCanReadInvoiceAsync(userId, invoice, cancellationToken);
        if (string.IsNullOrWhiteSpace(invoice.PdfStorageKey))
        {
            throw DomainException.Conflict("invoice_pdf_unavailable", "A PDF is not available for this invoice yet.");
        }

        var stream = await documents.OpenReadAsync(invoice.PdfStorageKey, cancellationToken);
        return new StoredInvoicePdf(stream, $"{invoice.Number}.pdf", "application/pdf");
    }

    public async Task SeedDefaultFinanceAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var rules = await store.ListRulesAsync(cancellationToken);
        if (rules.Count == 0)
        {
            store.AddRule(CommissionRule.Create(
                "Default platform commission",
                CommissionBasis.Percentage,
                10m,
                null,
                null,
                null,
                null,
                true,
                today.AddYears(-1),
                null,
                100,
                clock.UtcNow));
        }

        var tax = await store.FindActiveTaxAsync(today, cancellationToken);
        if (tax is null)
        {
            store.AddTaxRule(TaxRule.Create("GST0", "No GST applied by default", 0m, true, today.AddYears(-1), null, clock.UtcNow));
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkPaidInternalAsync(
        Payment payment,
        string providerPaymentId,
        Guid actorUserId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        payment.MarkPaid(providerPaymentId, clock.UtcNow);
        var appointment = await store.FindAppointmentAsync(payment.AppointmentId, true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        appointment.AttachPayment(payment.Id, nameof(PaymentStatus.Paid), payment.Amount, clock.UtcNow);
        await IssuePatientReceiptAsync(payment, appointment, cancellationToken);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.PaymentPaid, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await growth.OnPaymentPaidAsync(appointment.Id, payment.Id, cancellationToken);
        await PublishPaymentAsync(AutomationTriggerTypes.PaymentCompleted, payment, appointment, cancellationToken);
    }

    private async Task PublishPaymentAsync(string eventType, Payment payment, Appointment? appointment, CancellationToken cancellationToken)
    {
        try
        {
            await automation.PublishAsync(
                eventType,
                new
                {
                    paymentId = payment.Id,
                    appointmentId = payment.AppointmentId,
                    accountUserId = payment.AccountUserId,
                    userId = payment.AccountUserId,
                    amount = payment.Amount,
                    status = payment.Status.ToString()
                },
                $"{eventType}:{payment.Id:N}:{payment.UpdatedAtUtc.UtcTicks}",
                cancellationToken,
                subjectId: payment.Id,
                userId: payment.AccountUserId);
            _ = appointment;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish automation event {EventType} for payment {PaymentId}", eventType, payment.Id);
        }
    }

    private async Task IssuePatientReceiptAsync(Payment payment, Appointment appointment, CancellationToken cancellationToken)
    {
        var tax = await store.FindActiveTaxAsync(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken);
        var taxAmount = tax is null || tax.IsInclusive
            ? 0m
            : decimal.Round(payment.Amount * (tax.RatePercent / 100m), 2);
        var number = await store.NextNumberAsync("RCT", cancellationToken);
        var invoice = Invoice.Issue(
            number,
            InvoiceKind.PatientReceipt,
            appointment.PatientName,
            appointment.Reference,
            $"Consultation fee payment {payment.Number}",
            payment.Amount,
            taxAmount,
            payment.Amount + taxAmount,
            payment.Currency,
            tax?.Code,
            tax?.RatePercent ?? 0m,
            clock.UtcNow,
            appointment.Id,
            payment.Id,
            appointment.ClinicId,
            appointment.AccountUserId);
        var pdf = await invoicePdfs.GenerateAsync(invoice, cancellationToken);
        await using var stream = new MemoryStream(pdf);
        var stored = await documents.SaveAsync(stream, ".pdf", cancellationToken);
        invoice.AttachPdf(stored.StorageKey);
        store.AddInvoice(invoice);
        await store.AddAuditAsync(AuditLog.Record(null, AuditActions.InvoiceIssued, clock.UtcNow, null, null), cancellationToken);
    }

    private static void EnsureMethodAllowed(ClinicPaymentSettings settings, string method)
    {
        switch (settings.Policy)
        {
            case ClinicPaymentPolicy.PayAtClinicOnly when method != AppointmentPaymentMethods.PayAtClinic:
                throw DomainException.Conflict("online_payment_disabled", "This clinic only accepts payment at the clinic.");
            case ClinicPaymentPolicy.OnlineRequired when method == AppointmentPaymentMethods.PayAtClinic:
                throw DomainException.Conflict("online_payment_required", "This clinic requires online payment.");
            case ClinicPaymentPolicy.PartialAdvance when method is not (AppointmentPaymentMethods.PartialOnline or AppointmentPaymentMethods.PayOnline):
                throw DomainException.Conflict("advance_payment_required", "This clinic requires an online advance.");
        }
    }

    private async Task<ClinicPaymentSettings> EnsureSettingsAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var settings = await store.FindPaymentSettingsAsync(clinicId, true, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = ClinicPaymentSettings.CreateDefault(clinicId, clock.UtcNow);
        store.AddPaymentSettings(settings);
        await store.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task EnsureClinicOwnerAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(clinicId, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId == userId || await enterprise.UserHasOrgClinicAccessAsync(userId, clinicId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
    }

    private async Task EnsureClinicOwnerOrDoctorAsync(Guid userId, Appointment appointment, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(appointment.ClinicId, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId == userId || await enterprise.UserHasOrgClinicAccessAsync(userId, appointment.ClinicId, cancellationToken))
        {
            return;
        }

        var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken);
        if (doctor is null || doctor.Id != appointment.DoctorId)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }
    }

    private async Task EnsureCanReadInvoiceAsync(Guid userId, Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.AccountUserId == userId)
        {
            return;
        }

        if (invoice.ClinicId is Guid clinicId)
        {
            var clinic = await store.FindClinicAsync(clinicId, cancellationToken);
            if (clinic?.OwnerUserId == userId)
            {
                return;
            }
        }

        // Admin endpoints gate with admin.access / commissions.manage.
        throw DomainException.NotFound("invoice_not_found", "That invoice was not found.");
    }

    private static ClinicPaymentSettingsDetails MapSettings(ClinicPaymentSettings settings) =>
        new(settings.ClinicId, settings.Policy.ToString(), settings.AdvancePercent, settings.ChargeCommissionOnWalkIns);

    private static PaymentDetails MapPayment(Payment payment) =>
        new(
            payment.Id,
            payment.Number,
            payment.AppointmentId,
            payment.Channel.ToString(),
            payment.Status.ToString(),
            payment.Amount,
            payment.ConsultationFeeSnapshot,
            payment.GatewayFeeEstimated,
            payment.Currency,
            payment.Provider,
            payment.ProviderOrderId,
            payment.CheckoutPayload,
            payment.CreatedAtUtc,
            payment.PaidAtUtc);

    private static RefundDetails MapRefund(Refund refund) =>
        new(refund.Id, refund.Number, refund.PaymentId, refund.AppointmentId, refund.Amount, refund.Status.ToString(), refund.Reason, refund.CreatedAtUtc);

    private static CommissionRuleDetails MapRule(CommissionRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.Basis.ToString(),
            rule.Rate,
            rule.MinimumAmount,
            rule.MaximumAmount,
            rule.ClinicId,
            rule.DoctorId,
            rule.AppliesToPlatformBookingsOnly,
            rule.IsActive,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.Priority);

    private static SettlementDetails MapSettlement(SettlementBatch batch) =>
        new(
            batch.Id,
            batch.Number,
            batch.ClinicId,
            batch.Status.ToString(),
            batch.PeriodFrom,
            batch.PeriodTo,
            batch.GrossConsultationFees,
            batch.TotalCommission,
            batch.TotalGatewayFees,
            batch.NetPayable,
            batch.CreatedAtUtc,
            batch.FinalizedAtUtc,
            batch.Lines.Count);

    private static InvoiceDetails MapInvoice(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Number,
            invoice.Kind.ToString(),
            invoice.AppointmentId,
            invoice.PaymentId,
            invoice.ClinicId,
            invoice.BillToName,
            invoice.Description,
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.Total,
            invoice.AmountPaid,
            invoice.Outstanding,
            invoice.Status,
            !string.IsNullOrWhiteSpace(invoice.PdfStorageKey),
            invoice.IssuedAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
