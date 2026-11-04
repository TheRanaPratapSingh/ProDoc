namespace ProDoc.Application.Analytics;

public interface IAnalyticsReportExporter
{
    Task<byte[]> ExportExecutivePdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken);

    Task<byte[]> ExportOperationalPdfAsync(OperationalAnalytics data, CancellationToken cancellationToken);

    Task<byte[]> ExportFinancialPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken);

    Task<byte[]> ExportAppointmentsPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken);

    Task<byte[]> ExportGrowthPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken);
}
