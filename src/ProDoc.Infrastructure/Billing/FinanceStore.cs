using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Billing;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Billing;

public sealed class FinanceStore(ProDocDbContext db) : IFinanceStore
{
    public Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);

    public Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments.AsQueryable() : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
    }

    public Task<ClinicPaymentSettings?> FindPaymentSettingsAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ClinicPaymentSettings.AsQueryable() : db.ClinicPaymentSettings.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.ClinicId == clinicId, cancellationToken);
    }

    public void AddPaymentSettings(ClinicPaymentSettings settings) => db.ClinicPaymentSettings.Add(settings);

    public Task<Payment?> FindPaymentAsync(Guid paymentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Payments.AsQueryable() : db.Payments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
    }

    public Task<Payment?> FindPaymentByIdempotencyAsync(string key, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);

    public Task<Payment?> FindPaymentByProviderOrderAsync(string providerOrderId, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.ProviderOrderId == providerOrderId, cancellationToken);

    public Task<Payment?> FindPaidPaymentForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(
            item => item.AppointmentId == appointmentId && item.Status == PaymentStatus.Paid,
            cancellationToken);

    public void AddPayment(Payment payment) => db.Payments.Add(payment);

    public Task<bool> WebhookExistsAsync(string provider, string eventId, CancellationToken cancellationToken) =>
        db.PaymentWebhookEvents.AsNoTracking().AnyAsync(item => item.Provider == provider && item.EventId == eventId, cancellationToken);

    public void AddWebhook(PaymentWebhookEvent webhook) => db.PaymentWebhookEvents.Add(webhook);

    public void AddRefund(Refund refund) => db.Refunds.Add(refund);

    public Task<Refund?> FindRefundAsync(Guid refundId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Refunds.AsQueryable() : db.Refunds.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == refundId, cancellationToken);
    }

    public async Task<CommissionRule?> FindBestRuleAsync(
        Guid clinicId,
        Guid doctorId,
        DateOnly onDate,
        bool platformBooking,
        CancellationToken cancellationToken)
    {
        var rules = await db.CommissionRules.AsNoTracking()
            .Where(item => item.IsActive
                && item.EffectiveFrom <= onDate
                && (item.EffectiveTo == null || item.EffectiveTo >= onDate)
                && (item.ClinicId == null || item.ClinicId == clinicId)
                && (item.DoctorId == null || item.DoctorId == doctorId)
                && (!item.AppliesToPlatformBookingsOnly || platformBooking))
            .OrderByDescending(item => item.DoctorId != null)
            .ThenByDescending(item => item.ClinicId != null)
            .ThenBy(item => item.Priority)
            .ToListAsync(cancellationToken);
        return rules.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CommissionRule>> ListRulesAsync(CancellationToken cancellationToken) =>
        await db.CommissionRules.AsNoTracking().OrderBy(item => item.Priority).ThenBy(item => item.Name).ToListAsync(cancellationToken);

    public void AddRule(CommissionRule rule) => db.CommissionRules.Add(rule);

    public Task<CommissionSnapshot?> FindSnapshotByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.CommissionSnapshots.AsNoTracking().SingleOrDefaultAsync(item => item.AppointmentId == appointmentId, cancellationToken);

    public async Task<IReadOnlyList<CommissionSnapshot>> ListUnsettledSnapshotsAsync(
        Guid clinicId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        return await db.CommissionSnapshots
            .Where(item => item.ClinicId == clinicId
                && item.SettlementBatchId == null
                && item.CapturedAtUtc >= fromUtc
                && item.CapturedAtUtc <= toUtc
                && item.CommissionAmount > 0)
            .OrderBy(item => item.CapturedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void AddSnapshot(CommissionSnapshot snapshot) => db.CommissionSnapshots.Add(snapshot);

    public Task<TaxRule?> FindActiveTaxAsync(DateOnly onDate, CancellationToken cancellationToken) =>
        db.TaxRules.AsNoTracking()
            .Where(item => item.IsActive && item.EffectiveFrom <= onDate && (item.EffectiveTo == null || item.EffectiveTo >= onDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddTaxRule(TaxRule rule) => db.TaxRules.Add(rule);

    public void AddInvoice(Invoice invoice) => db.Invoices.Add(invoice);

    public Task<Invoice?> FindInvoiceAsync(Guid invoiceId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Invoices.AsQueryable() : db.Invoices.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
    }

    public async Task<string> NextNumberAsync(string prefix, CancellationToken cancellationToken)
    {
        var day = DateTime.UtcNow.ToString("yyyyMMdd");
        var stem = $"{prefix}{day}";
        var latest = prefix switch
        {
            "PAY" => await db.Payments.AsNoTracking().Where(item => item.Number.StartsWith(stem)).OrderByDescending(item => item.Number).Select(item => item.Number).FirstOrDefaultAsync(cancellationToken),
            "RFD" => await db.Refunds.AsNoTracking().Where(item => item.Number.StartsWith(stem)).OrderByDescending(item => item.Number).Select(item => item.Number).FirstOrDefaultAsync(cancellationToken),
            "RCT" or "INV" => await db.Invoices.AsNoTracking().Where(item => item.Number.StartsWith(stem)).OrderByDescending(item => item.Number).Select(item => item.Number).FirstOrDefaultAsync(cancellationToken),
            "STL" => await db.SettlementBatches.AsNoTracking().Where(item => item.Number.StartsWith(stem)).OrderByDescending(item => item.Number).Select(item => item.Number).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
        var next = 1;
        if (latest is not null && latest.Length > stem.Length && int.TryParse(latest[stem.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{stem}{next:D4}";
    }

    public void AddSettlement(SettlementBatch batch) => db.SettlementBatches.Add(batch);

    public Task<SettlementBatch?> FindSettlementAsync(Guid settlementId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.SettlementBatches.AsQueryable() : db.SettlementBatches.AsNoTracking();
        return query.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == settlementId, cancellationToken);
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<ClinicFinanceDashboard> BuildClinicDashboardAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var commissions = await db.CommissionSnapshots.AsNoTracking()
            .Where(item => item.ClinicId == clinicId)
            .OrderByDescending(item => item.CapturedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Where(item => item.ClinicId == clinicId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        var paymentIds = await db.Payments.AsNoTracking().Where(item => item.ClinicId == clinicId).Select(item => item.Id).ToListAsync(cancellationToken);
        var refunds = paymentIds.Count == 0
            ? 0m
            : await db.Refunds.AsNoTracking()
                .Where(item => paymentIds.Contains(item.PaymentId) && item.Status == RefundStatus.Completed)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var allCommissions = await db.CommissionSnapshots.AsNoTracking().Where(item => item.ClinicId == clinicId).ToListAsync(cancellationToken);
        var online = payments.Where(item => item.Channel == PaymentChannel.OnlineGateway && item.Status == PaymentStatus.Paid).Sum(item => item.Amount);
        var clinicPay = payments.Where(item => item.Channel == PaymentChannel.PayAtClinic && item.Status == PaymentStatus.Paid).Sum(item => item.Amount);
        return new ClinicFinanceDashboard(
            clinicId,
            allCommissions.Sum(item => item.ConsultationFee),
            online,
            clinicPay,
            allCommissions.Sum(item => item.CommissionAmount),
            refunds,
            allCommissions.Where(item => item.SettlementBatchId == null).Sum(item => item.ClinicPayableAmount),
            allCommissions.Sum(item => item.ClinicPayableAmount),
            commissions.Select(MapSnapshot).ToArray(),
            payments.Select(MapPayment).ToArray());
    }

    public async Task<AdminFinanceDashboard> BuildAdminDashboardAsync(CancellationToken cancellationToken)
    {
        var snapshots = await db.CommissionSnapshots.AsNoTracking().ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking().Where(item => item.Status == PaymentStatus.Paid).ToListAsync(cancellationToken);
        var refunds = await db.Refunds.AsNoTracking().Where(item => item.Status == RefundStatus.Completed).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var settlements = await db.SettlementBatches.AsNoTracking().Include(item => item.Lines)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return new AdminFinanceDashboard(
            snapshots.Where(item => !item.IsWalkInExcluded).Sum(item => item.ConsultationFee),
            payments.Where(item => item.Channel == PaymentChannel.OnlineGateway).Sum(item => item.Amount),
            snapshots.Sum(item => item.CommissionAmount),
            refunds,
            snapshots.Where(item => item.SettlementBatchId == null).Sum(item => item.ClinicPayableAmount),
            snapshots.Count(item => !item.IsWalkInExcluded),
            settlements.Select(MapSettlement).ToArray());
    }

    public async Task<IReadOnlyList<SettlementDetails>> ListSettlementsAsync(Guid? clinicId, CancellationToken cancellationToken)
    {
        var query = db.SettlementBatches.AsNoTracking().Include(item => item.Lines).AsQueryable();
        if (clinicId is not null)
        {
            query = query.Where(item => item.ClinicId == clinicId);
        }

        var rows = await query.OrderByDescending(item => item.CreatedAtUtc).Take(50).ToListAsync(cancellationToken);
        return rows.Select(MapSettlement).ToArray();
    }

    private static CommissionSnapshotDetails MapSnapshot(CommissionSnapshot item) =>
        new(
            item.Id,
            item.AppointmentId,
            item.ClinicId,
            item.DoctorId,
            item.RuleNameSnapshot,
            item.ConsultationFee,
            item.CommissionAmount,
            item.ClinicPayableAmount,
            item.GatewayFeeSnapshot,
            item.IsWalkInExcluded,
            item.CapturedAtUtc,
            item.SettlementBatchId);

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
}
