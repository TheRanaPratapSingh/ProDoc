using Microsoft.Extensions.DependencyInjection;
using ProDoc.Application.Admin;
using ProDoc.Application.Analytics;
using ProDoc.Application.Assistant;
using ProDoc.Application.Automation;
using ProDoc.Application.Appointments;
using ProDoc.Application.Auth;
using ProDoc.Application.Billing;
using ProDoc.Application.Clinical;
using ProDoc.Application.Enterprise;
using ProDoc.Application.Growth;
using ProDoc.Application.Integrations;
using ProDoc.Application.Mobile;
using ProDoc.Application.Providers;
using ProDoc.Application.Support;
using ProDoc.Application.Queue;
using ProDoc.Application.Reviews;
using ProDoc.Application.Partners;
using ProDoc.Application.SaaS;
using ProDoc.Application.Scheduling;

namespace ProDoc.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<DoctorOnboardingService>();
        services.AddScoped<ClinicOnboardingService>();
        services.AddScoped<ProviderDocumentService>();
        services.AddScoped<ProviderDirectoryService>();
        services.AddScoped<VerificationReviewService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<DiscoveryService>();
        services.AddScoped<ClinicOperationsService>();
        services.AddScoped<FavouriteService>();
        services.AddScoped<BookingService>();
        services.AddScoped<IClinicAccess, ClinicAccessService>();
        services.AddScoped<StaffService>();
        services.AddScoped<QueueService>();
        services.AddScoped<MedicalService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<AdminPlatformService>();
        services.AddScoped<AssistantService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<GrowthService>();
        services.AddScoped<EnterpriseService>();
        services.AddSingleton<FhirMappingService>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<MobileDeviceService>();
        services.AddScoped<SupportService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<SaaSService>();
        services.AddScoped<ISaaSService>(provider => provider.GetRequiredService<SaaSService>());
        services.AddScoped<ITenantEntitlementGuard>(provider => provider.GetRequiredService<SaaSService>());
        services.AddScoped<AutomationEngine>();
        services.AddScoped<IAutomationEventBus, AutomationEventBus>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IPartnerWebhookPublisher, PartnerWebhookPublisher>();
        services.AddScoped<IPartnerService, PartnerService>();
        return services;
    }
}
