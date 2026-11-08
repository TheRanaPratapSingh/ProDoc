using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Billing;

public interface IFinanceStore
{
    Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken);

    Task<ClinicPaymentSettings?> FindPaymentSettingsAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken);

    void AddPaymentSettings(ClinicPaymentSettings settings);

    Task<Payment?> FindPaymentAsync(Guid paymentId, bool tracking, CancellationToken cancellationToken);

    Task<Payment?> FindPaymentByIdempotencyAsync(string key, CancellationToken cancellationToken);

    Task<Payment?> FindPaymentByProviderOrderAsync(string providerOrderId, CancellationToken cancellationToken);

    Task<Payment?> FindPaidPaymentForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    void AddPayment(Payment payment);

    Task<bool> WebhookExistsAsync(string provider, string eventId, CancellationToken cancellationToken);

    void AddWebhook(PaymentWebhookEvent webhook);

    void AddRefund(Refund refund);

    Task<Refund?> FindRefundAsync(Guid refundId, bool tracking, CancellationToken cancellationToken);

    Task<CommissionRule?> FindBestRuleAsync(Guid clinicId, Guid doctorId, DateOnly onDate, bool platformBooking, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommissionRule>> ListRulesAsync(CancellationToken cancellationToken);

    void AddRule(CommissionRule rule);

    Task<CommissionSnapshot?> FindSnapshotByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommissionSnapshot>> ListUnsettledSnapshotsAsync(Guid clinicId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    void AddSnapshot(CommissionSnapshot snapshot);

    Task<TaxRule?> FindActiveTaxAsync(DateOnly onDate, CancellationToken cancellationToken);

    void AddTaxRule(TaxRule rule);

    void AddInvoice(Invoice invoice);

    Task<Invoice?> FindInvoiceAsync(Guid invoiceId, bool tracking, CancellationToken cancellationToken);

    Task<string> NextNumberAsync(string prefix, CancellationToken cancellationToken);

    void AddSettlement(SettlementBatch batch);

    Task<SettlementBatch?> FindSettlementAsync(Guid settlementId, bool tracking, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<ClinicFinanceDashboard> BuildClinicDashboardAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<AdminFinanceDashboard> BuildAdminDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SettlementDetails>> ListSettlementsAsync(Guid? clinicId, CancellationToken cancellationToken);

    Task<DoctorEarningsDashboard> BuildDoctorEarningsAsync(
        Guid doctorId,
        Guid? clinicId,
        DateOnly today,
        CancellationToken cancellationToken);
}

public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(Invoice invoice, CancellationToken cancellationToken);
}
