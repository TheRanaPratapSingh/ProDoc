using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Automation;

public sealed class AutomationOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<AutomationOutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Expected cancellation during startup must not bubble as a host fault (TaskCanceledException at Delay).
        if (!await DelayQuietlyAsync(StartupDelay, stoppingToken))
        {
            return;
        }

        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var automation = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                await automation.ProcessDueAsync(stoppingToken);
                await EmitScheduledScansAsync(scope.ServiceProvider, stoppingToken);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                logger.LogError(
                    ex,
                    "Automation outbox processor cycle failed (attempt {Attempt}). Events remain in the outbox for retry.",
                    consecutiveFailures);
            }

            var delay = consecutiveFailures == 0
                ? IdleDelay
                : TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, IdleDelay.TotalSeconds * Math.Pow(2, Math.Min(consecutiveFailures, 5))));
            if (!await DelayQuietlyAsync(delay, stoppingToken))
            {
                break;
            }
        }
    }

    private static async Task EmitScheduledScansAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var bus = services.GetRequiredService<IAutomationEventBus>();
        var db = services.GetRequiredService<ProDocDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var renewals = await db.OrganizationSubscriptions.AsNoTracking()
            .Where(item => item.CurrentPeriodEndUtc != null
                && item.CurrentPeriodEndUtc > now
                && item.CurrentPeriodEndUtc <= now.AddDays(7)
                && item.Status == OrganizationSubscriptionStatus.Active)
            .OrderBy(item => item.CurrentPeriodEndUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var sub in renewals)
        {
            await bus.PublishAsync(
                AutomationTriggerTypes.SubscriptionRenewalApproaching,
                new
                {
                    organizationId = sub.OrganizationId,
                    planCode = sub.PlanCode,
                    periodEndUtc = sub.CurrentPeriodEndUtc
                },
                $"saas-renewal:{sub.OrganizationId:N}:{sub.CurrentPeriodEndUtc:yyyyMMdd}",
                cancellationToken,
                organizationId: sub.OrganizationId,
                subjectId: sub.Id);
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var soon = today.AddDays(30);
        var registrations = await db.Set<DoctorRegistration>().AsNoTracking()
            .Where(item => item.ExpiresOn != null && item.ExpiresOn <= soon && item.ExpiresOn >= today)
            .OrderBy(item => item.ExpiresOn)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var registration in registrations)
        {
            var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.Id == registration.DoctorId, cancellationToken);
            if (doctor is null)
            {
                continue;
            }

            await bus.PublishAsync(
                AutomationTriggerTypes.DoctorRegistrationExpiring,
                new
                {
                    doctorId = doctor.Id,
                    userId = doctor.UserId,
                    expiresOn = registration.ExpiresOn
                },
                $"reg-expiry:{registration.DoctorId:N}:{registration.ExpiresOn:yyyyMMdd}",
                cancellationToken,
                subjectId: doctor.Id,
                userId: doctor.UserId);
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
