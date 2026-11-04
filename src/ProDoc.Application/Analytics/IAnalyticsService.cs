using ProDoc.Application.Providers;

namespace ProDoc.Application.Analytics;

public interface IAnalyticsService
{
    Task<ExecutiveAnalytics> GetExecutiveAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<OperationalAnalytics> GetOperationalAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<PredictiveInsights> GetPredictiveAsync(CancellationToken cancellationToken);

    Task<AnalyticsExportResult> ExportAsync(
        string reportType,
        string format,
        DateOnly from,
        DateOnly to,
        Guid actorUserId,
        RequestMetadata metadata,
        CancellationToken cancellationToken);
}
