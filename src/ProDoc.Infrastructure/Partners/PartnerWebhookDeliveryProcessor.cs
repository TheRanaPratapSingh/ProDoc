using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Partners;

namespace ProDoc.Infrastructure.Partners;

public sealed class PartnerWebhookDeliveryProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<PartnerWebhookDeliveryProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Expected cancellation during startup delay must not bubble as a host fault.
        if (!await DelayQuietlyAsync(StartupDelay, stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var partners = scope.ServiceProvider.GetRequiredService<IPartnerService>();
                await partners.ProcessWebhookDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Keep the API process alive; only unexpected cycle failures are logged.
                logger.LogWarning(ex, "Partner webhook delivery processor cycle failed.");
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
