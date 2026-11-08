using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Application.Analytics;
using ProDoc.Application.Appointments;
using ProDoc.Application.Automation;
using ProDoc.Application.Assistant;
using ProDoc.Application.Billing;
using ProDoc.Application.Clinical;
using ProDoc.Application.Enterprise;
using ProDoc.Application.Growth;
using ProDoc.Application.Integrations;
using ProDoc.Application.Mobile;
using ProDoc.Application.Notifications;
using ProDoc.Application.Queue;
using ProDoc.Application.Reviews;
using ProDoc.Application.Partners;
using ProDoc.Application.SaaS;
using ProDoc.Application.Support;
using ProDoc.Infrastructure.Enterprise;
using ProDoc.Infrastructure.Partners;
using ProDoc.Infrastructure.SaaS;
using ProDoc.Infrastructure.Growth;
using ProDoc.Infrastructure.Integrations;
using ProDoc.Infrastructure.Mobile;
using ProDoc.Infrastructure.Notifications;
using ProDoc.Infrastructure.Queue;
using ProDoc.Infrastructure.Support;
using ProDoc.Application.Auth;
using ProDoc.Application.Providers;
using ProDoc.Application.Scheduling;
using ProDoc.Infrastructure.Admin;
using ProDoc.Infrastructure.Analytics;
using ProDoc.Infrastructure.Appointments;
using ProDoc.Infrastructure.Automation;
using ProDoc.Infrastructure.Assistant;
using ProDoc.Infrastructure.Auth;
using ProDoc.Infrastructure.Billing;
using ProDoc.Infrastructure.Clinical;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;
using ProDoc.Infrastructure.Providers;
using ProDoc.Infrastructure.Reviews;
using ProDoc.Infrastructure.Scheduling;
using ProDoc.Infrastructure.Time;

namespace ProDoc.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "ProDoc";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:ProDoc is required.");
        }

        var sms = configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>() ?? new SmsOptions();
        if (!environment.IsDevelopment()
            && !string.Equals(sms.Provider, "Http", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SMS delivery is not configured. Set Sms:Provider=Http with Sms:EndpointUrl (and ApiKey) outside Development, or run Development.");
        }

        if (!environment.IsDevelopment()
            && string.Equals(sms.Provider, "Http", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(sms.EndpointUrl))
        {
            throw new InvalidOperationException("Sms:EndpointUrl is required when Sms:Provider=Http.");
        }

        services.AddDbContext<ProDocDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ProDocDbContext).Assembly.GetName().Name);
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                sql.CommandTimeout(30);
            }));

        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = false)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ProDocDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<IdentitySeeder>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRefreshTokenFactory, RefreshTokenFactory>();
        services.AddSingleton<IOtpCodeProtector, HmacOtpCodeProtector>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddOptions<SmsOptions>().Bind(configuration.GetSection(SmsOptions.SectionName));
        services.AddHttpClient("ProDocSms", client => client.Timeout = TimeSpan.FromSeconds(15));
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IOtpSender, DevelopmentOtpSender>();
        }
        else
        {
            services.AddSingleton<IOtpSender, HttpSmsOtpSender>();
        }

        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IStaffInvitationStore, StaffInvitationStore>();
        services.AddScoped<IAdminClinicStore, AdminClinicStore>();
        services.AddScoped<IProviderStore, ProviderStore>();
        services.AddScoped<IScheduleStore, ScheduleStore>();
        services.AddScoped<AppointmentStore>();
        services.AddScoped<IAppointmentStore>(provider => provider.GetRequiredService<AppointmentStore>());
        services.AddScoped<ISlotOccupancy>(provider => provider.GetRequiredService<AppointmentStore>());
        services.AddScoped<IQueueStore, QueueStore>();
        services.AddScoped<IQueueRealtime, SignalRQueueRealtime>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<INotificationRealtime, SignalRNotificationRealtime>();
        services.AddScoped<IMedicalStore, MedicalStore>();
        services.AddScoped<IFollowUpStore, FollowUpStore>();
        services.AddSingleton<IPrescriptionPdfGenerator, PrescriptionPdfGenerator>();
        services.AddScoped<IFinanceStore, FinanceStore>();
        services.AddScoped<IAdminStore, AdminStore>();
        services.AddScoped<IAssistantStore, AssistantStore>();
        services.AddScoped<IReviewStore, ReviewStore>();
        services.AddScoped<IGrowthStore, GrowthStore>();
        services.AddScoped<IEnterpriseStore, EnterpriseStore>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddScoped<IMobileStore, MobileStore>();
        services.AddScoped<ISupportStore, SupportStore>();
        services.AddScoped<IAnalyticsStore, AnalyticsStore>();
        services.AddSingleton<IAnalyticsReportExporter, AnalyticsReportExporter>();
        services.AddScoped<ISaaSStore, SaaSStore>();
        services.AddScoped<IAutomationStore, AutomationStore>();
        services.AddSingleton<IAutomationMessenger, DevelopmentAutomationMessenger>();
        services.AddScoped<IPartnerStore, PartnerStore>();
        services.AddSingleton<IPartnerAccessTokenIssuer, PartnerAccessTokenIssuer>();
        services.AddScoped<IPartnerWebhookSender, PartnerWebhookSender>();
        services.AddHttpClient(PartnerWebhookSender.HttpClientName, client =>
        {
            // Slightly above per-attempt CancelAfter so HttpClient doesn't race our linked CTS.
            client.Timeout = PartnerWebhookSender.RequestTimeout + TimeSpan.FromSeconds(5);
        });
        services.AddMemoryCache();
        services.AddHostedService<AnalyticsCacheWarmHostedService>();
        services.AddHostedService<AutomationOutboxProcessor>();
        services.AddHostedService<PartnerWebhookDeliveryProcessor>();
        services.AddSingleton<ISupportFileStore, LocalSupportFileStore>();
        services.AddSingleton<IPushNotificationSender, DevelopmentPushSender>();
        services.AddOptions<IntegrationOptions>().Bind(configuration.GetSection(IntegrationOptions.SectionName));
        services.AddSingleton<IAbdmIntegrationAdapter, SandboxAbdmAdapter>();
        services.AddSingleton<ILabIntegrationAdapter, SandboxLabAdapter>();
        services.AddSingleton<IPharmacyIntegrationAdapter, SandboxPharmacyAdapter>();
        services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();
        services.AddOptions<PaymentGatewayOptions>().Bind(configuration.GetSection(PaymentGatewayOptions.SectionName));
        services.AddHttpClient(nameof(RazorpayPaymentGateway));
        var paymentProvider = configuration.GetSection(PaymentGatewayOptions.SectionName).Get<PaymentGatewayOptions>()?.Provider ?? "Development";
        if (string.Equals(paymentProvider, "Razorpay", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPaymentGateway, RazorpayPaymentGateway>();
        }
        else
        {
            services.AddSingleton<IPaymentGateway, DevelopmentPaymentGateway>();
        }

        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName));
        services.AddSingleton<DevelopmentAiProvider>();
        services.AddHttpClient(nameof(OpenAiCompatibleProvider));
        var aiProvider = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>()?.Provider ?? "Development";
        if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
        }
        else
        {
            services.AddSingleton<IAiProvider>(provider => provider.GetRequiredService<DevelopmentAiProvider>());
        }

        services.AddSignalR();
        services.AddSingleton<IPlaceLinkBuilder, MapLinkBuilder>();
        services.AddOptions<MapOptions>().Bind(configuration.GetSection(MapOptions.SectionName));
        services.AddOptions<AppointmentShareOptions>().Bind(configuration.GetSection(AppointmentShareOptions.SectionName));
        services.AddScoped<ProviderSeeder>();
        services.AddSingleton<IUploadScanner, SignatureAllowListScanner>();
        services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
        services.AddSingleton<IVerificationNotifier, LoggingVerificationNotifier>();
        services.AddOptions<ProviderFileOptions>().Bind(configuration.GetSection(ProviderFileOptions.SectionName));

        services.AddOptions<AuthCryptoOptions>()
            .Bind(configuration.GetSection(AuthCryptoOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthCryptoOptions>, AuthCryptoOptionsValidator>();
        services.AddSingleton<IValidateOptions<SessionOptions>, SessionOptionsValidator>();

        var crypto = configuration.GetSection(AuthCryptoOptions.SectionName).Get<AuthCryptoOptions>() ?? new AuthCryptoOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = crypto.Issuer,
                    ValidAudience = crypto.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(crypto.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = "role",
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            })
            .AddJwtBearer(PartnerAccessTokenIssuer.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = crypto.Issuer,
                    ValidAudience = PartnerAccessTokenIssuer.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(crypto.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
                };
            });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveAccountRequirement())
                .Build();
            options.AddPolicy("PartnerApi", policy =>
            {
                policy.AuthenticationSchemes.Add(PartnerAccessTokenIssuer.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("token_use", PartnerAccessTokenIssuer.TokenUse);
            });
        });
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveAccountHandler>();
        services.AddHealthChecks()
            .AddDbContextCheck<ProDocDbContext>("database", tags: ["ready"]);
        return services;
    }

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        bool migrate,
        CancellationToken cancellationToken)
    {
        await using (var migrateScope = services.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<ProDocDbContext>();
            if (migrate)
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
        }

        // Each seeder gets an isolated scope so EF change-tracker / RowVersion state cannot leak across seeds.
        await SeedInScopeAsync<IdentitySeeder>(services, (seeder, ct) => seeder.SeedAsync(ct), cancellationToken);
        await SeedInScopeAsync<ProviderSeeder>(services, (seeder, ct) => seeder.SeedAsync(ct), cancellationToken);
        await SeedInScopeAsync<FinanceService>(services, (seeder, ct) => seeder.SeedDefaultFinanceAsync(ct), cancellationToken);
        await SeedInScopeAsync<AdminPlatformService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
        await SeedInScopeAsync<GrowthService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
        await SeedInScopeAsync<IntegrationService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
        await SeedInScopeAsync<SupportService>(services, (seeder, ct) => seeder.SeedHelpCenterAsync(ct), cancellationToken);
        await SeedInScopeAsync<ISaaSService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
        await SeedInScopeAsync<IAutomationService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
        await SeedInScopeAsync<IPartnerService>(services, (seeder, ct) => seeder.SeedDefaultsAsync(ct), cancellationToken);
    }

    private static async Task SeedInScopeAsync<TService>(
        IServiceProvider services,
        Func<TService, CancellationToken, Task> seed,
        CancellationToken cancellationToken)
        where TService : notnull
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<TService>();
        await seed(seeder, cancellationToken);
    }
}
