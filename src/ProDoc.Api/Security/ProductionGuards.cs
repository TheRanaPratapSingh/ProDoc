using ProDoc.Application.Billing;
using ProDoc.Infrastructure.Auth;

namespace ProDoc.Api.Security;

public static class ProductionGuards
{
    public static void EnsureSafeForEnvironment(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var auth = configuration.GetSection(AuthCryptoOptions.SectionName).Get<AuthCryptoOptions>() ?? new AuthCryptoOptions();
        if (string.IsNullOrWhiteSpace(auth.SigningKey) || auth.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Auth:SigningKey must be configured with at least 32 characters outside Development.");
        }

        if (string.IsNullOrWhiteSpace(auth.OtpHashKey) || auth.OtpHashKey.Length < 32)
        {
            throw new InvalidOperationException("Auth:OtpHashKey must be configured with at least 32 characters outside Development.");
        }

        var connection = configuration.GetConnectionString("ProDoc");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("ConnectionStrings:ProDoc is required outside Development.");
        }

        if (connection.Contains("AKKI\\SQLEXPRESS", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("localhost\\SQLEXPRESS", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Developer machine connection strings cannot be used outside Development.");
        }

        var payments = configuration.GetSection(PaymentGatewayOptions.SectionName).Get<PaymentGatewayOptions>() ?? new PaymentGatewayOptions();
        if (environment.IsProduction()
            && string.Equals(payments.Provider, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payments:Provider=Development is not allowed in Production. Configure Razorpay (or another live provider) explicitly.");
        }

        var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:Origins must list at least one allowed origin outside Development.");
        }

        if (origins.Any(origin => origin == "*"))
        {
            throw new InvalidOperationException("Cors:Origins must not use wildcard origins.");
        }
    }

    public static bool ShouldMigrate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return true;
        }

        var flag = configuration["PRODOC_MIGRATE"] ?? configuration["ProDoc:MigrateOnStartup"];
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }
}
