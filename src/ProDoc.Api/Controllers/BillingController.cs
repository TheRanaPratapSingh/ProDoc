using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Billing;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class BillingController(FinanceService finance) : ControllerBase
{
    [HasPermission(PermissionCodes.ClinicManage)]
    [HttpGet("clinics/{clinicId:guid}/payment-settings")]
    public Task<ClinicPaymentSettingsDetails> GetPaymentSettings(Guid clinicId, CancellationToken cancellationToken) =>
        finance.GetClinicPaymentSettingsAsync(User.GetUserId(), clinicId, cancellationToken);

    [HasPermission(PermissionCodes.ClinicManage)]
    [HttpPut("clinics/{clinicId:guid}/payment-settings")]
    public Task<ClinicPaymentSettingsDetails> PutPaymentSettings(Guid clinicId, UpdateClinicPaymentSettingsRequest request, CancellationToken cancellationToken) =>
        finance.UpdateClinicPaymentSettingsAsync(
            User.GetUserId(),
            clinicId,
            request.Policy,
            request.AdvancePercent,
            request.ChargeCommissionOnWalkIns,
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("appointments/{appointmentId:guid}/payments")]
    public Task<PaymentDetails> CreatePayment(Guid appointmentId, CreatePaymentRequest request, CancellationToken cancellationToken) =>
        finance.CreateAppointmentPaymentAsync(
            User.GetUserId(),
            appointmentId,
            request.PaymentMethod,
            request.IdempotencyKey,
            Meta(),
            cancellationToken,
            request.CouponCode,
            request.UseRewardCredit);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("payments/{paymentId:guid}/verify")]
    public Task<PaymentDetails> Verify(Guid paymentId, VerifyPaymentRequest request, CancellationToken cancellationToken) =>
        finance.VerifyPaymentAsync(User.GetUserId(), paymentId, request.ProviderPaymentId, Meta(), cancellationToken);

    [AllowAnonymous]
    [EnableRateLimiting("webhooks")]
    [HttpPost("payments/webhooks/{provider}")]
    public async Task<IActionResult> Webhook(string provider, CancellationToken cancellationToken)
    {
        _ = provider;
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await finance.HandleWebhookAsync(body, headers, cancellationToken);
        return Ok();
    }

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("appointments/{appointmentId:guid}/payments/clinic-collection")]
    public Task<PaymentDetails> ClinicCollection(Guid appointmentId, CancellationToken cancellationToken) =>
        finance.RecordClinicCollectionAsync(User.GetUserId(), appointmentId, Meta(), cancellationToken);

    [Authorize]
    [HttpPost("payments/{paymentId:guid}/refunds")]
    public Task<RefundDetails> Refund(Guid paymentId, RefundPaymentRequest request, CancellationToken cancellationToken) =>
        finance.RequestRefundAsync(User.GetUserId(), paymentId, request.Amount, request.Reason, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.ClinicManage)]
    [HttpGet("clinics/{clinicId:guid}/finance")]
    public Task<ClinicFinanceDashboard> ClinicFinance(Guid clinicId, CancellationToken cancellationToken) =>
        finance.ClinicDashboardAsync(User.GetUserId(), clinicId, cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpGet("admin/finance")]
    public Task<AdminFinanceDashboard> AdminFinance(CancellationToken cancellationToken) =>
        finance.AdminDashboardAsync(cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpGet("admin/commission-rules")]
    public Task<IReadOnlyList<CommissionRuleDetails>> ListRules(CancellationToken cancellationToken) =>
        finance.ListCommissionRulesAsync(cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpPost("admin/commission-rules")]
    public Task<CommissionRuleDetails> CreateRule(CreateCommissionRuleRequest request, CancellationToken cancellationToken) =>
        finance.CreateCommissionRuleAsync(
            User.GetUserId(),
            request.Name,
            request.Basis,
            request.Rate,
            request.MinimumAmount,
            request.MaximumAmount,
            request.ClinicId,
            request.DoctorId,
            request.AppliesToPlatformBookingsOnly,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Priority,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpPost("admin/settlements")]
    public Task<SettlementDetails> CreateSettlement(CreateSettlementRequest request, CancellationToken cancellationToken) =>
        finance.CreateSettlementAsync(User.GetUserId(), request.ClinicId, request.PeriodFrom, request.PeriodTo, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpGet("admin/settlements")]
    public Task<IReadOnlyList<SettlementDetails>> ListSettlements([FromQuery] Guid? clinicId, CancellationToken cancellationToken) =>
        finance.ListSettlementsAsync(clinicId, cancellationToken);

    [HttpGet("invoices/{invoiceId:guid}")]
    public Task<InvoiceDetails> GetInvoice(Guid invoiceId, CancellationToken cancellationToken) =>
        finance.GetInvoiceAsync(User.GetUserId(), invoiceId, cancellationToken);

    [HttpGet("invoices/{invoiceId:guid}/pdf")]
    public async Task<IActionResult> InvoicePdf(Guid invoiceId, CancellationToken cancellationToken)
    {
        var pdf = await finance.DownloadInvoicePdfAsync(User.GetUserId(), invoiceId, cancellationToken);
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
