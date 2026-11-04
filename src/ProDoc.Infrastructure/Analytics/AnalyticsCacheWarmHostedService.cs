using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Analytics;

namespace ProDoc.Infrastructure.Analytics;

/// <summary>
/// Pre-warms frequently requested platform analytics off the booking request path.
/// Failures are logged and ignored so transactional APIs are unaffected.
/// </summary>
public sealed class AnalyticsCacheWarmHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsCacheWarmHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(4);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await DelayQuietlyAsync(StartupDelay, stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                var to = DateOnly.FromDateTime(DateTime.UtcNow);
                var from = to.AddDays(-30);
                await analytics.GetExecutiveAsync(from, to, stoppingToken);
                await analytics.GetOperationalAsync(from, to, stoppingToken);
                await analytics.GetPredictiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Analytics cache warm skipped.");
            }

            if (!await DelayQuietlyAsync(IdleDelay, stoppingToken))
            {
                break;
            }
        }
    }

    private static async Task<bool> DelayQuietlyAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
