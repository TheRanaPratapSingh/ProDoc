using ProDoc.Application.Analytics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProDoc.Infrastructure.Analytics;

public sealed class AnalyticsReportExporter : IAnalyticsReportExporter
{
    static AnalyticsReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> ExportExecutivePdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken) =>
        Task.FromResult(Build(cancellationToken, "Executive analytics (actuals)", column =>
        {
            Metric(column, "Patients", data.TotalPatients.ToString());
            Metric(column, "Verified doctors", data.VerifiedDoctors.ToString());
            Metric(column, "Clinics", data.TotalClinics.ToString());
            Metric(column, "Appointments", data.AppointmentsInRange.ToString());
            Metric(column, "Completion rate %", data.CompletionRatePercent.ToString("0.0"));
            Metric(column, "Cancellation rate %", data.CancellationRatePercent.ToString("0.0"));
            Metric(column, "Missed/expired rate %", data.MissedRatePercent.ToString("0.0"));
            Metric(column, "Gross booking (INR)", data.GrossBookingValue.ToString("0.00"));
            Metric(column, "Commission (INR)", data.CommissionRevenue.ToString("0.00"));
            Metric(column, "Refunds (INR)", data.Refunds.ToString("0.00"));
            Metric(column, "Pending payouts (INR)", data.PendingPayouts.ToString("0.00"));
            Metric(column, "Retention %", data.PatientRetentionPercent.ToString("0.0"));
            column.Item().PaddingTop(12).Text(data.DataFreshnessNote).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().Text(data.TimeZoneNote).FontSize(9).FontColor(Colors.Grey.Darken1);
        }));

    public Task<byte[]> ExportOperationalPdfAsync(OperationalAnalytics data, CancellationToken cancellationToken) =>
        Task.FromResult(Build(cancellationToken, "Operational analytics (actuals)", column =>
        {
            Metric(column, "Avg wait (min)", data.AverageWaitMinutes?.ToString("0.##") ?? "n/a");
            Metric(column, "Avg consult (min)", data.AverageConsultationMinutes?.ToString("0.##") ?? "n/a");
            Metric(column, "Slot utilization %", data.SlotUtilizationPercent.ToString("0.0"));
            Metric(column, "Walk-in", data.WalkInBookings.ToString());
            Metric(column, "Online", data.OnlineBookings.ToString());
            column.Item().PaddingTop(8).Text("Top peak hours (UTC)").SemiBold();
            foreach (var row in data.PeakBookingHoursUtc.Take(6))
            {
                column.Item().Text($"{row.Label}: {row.Count}");
            }

            column.Item().PaddingTop(12).Text(data.MethodologyNote).FontSize(9).FontColor(Colors.Grey.Darken1);
        }));

    public Task<byte[]> ExportFinancialPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken) =>
        Task.FromResult(Build(cancellationToken, "Financial analytics (actuals)", column =>
        {
            Metric(column, "Gross booking (INR)", data.GrossBookingValue.ToString("0.00"));
            Metric(column, "Commission (INR)", data.CommissionRevenue.ToString("0.00"));
            Metric(column, "Refunds (INR)", data.Refunds.ToString("0.00"));
            Metric(column, "Pending payouts (INR)", data.PendingPayouts.ToString("0.00"));
            column.Item().PaddingTop(8).Text("Commission by day").SemiBold();
            foreach (var row in data.CommissionTrend.Take(31))
            {
                column.Item().Text($"{row.Label}: {row.Amount:0.00}");
            }

            column.Item().PaddingTop(12).Text("Figures are captured transactions only — not forecasts.").FontSize(9).FontColor(Colors.Grey.Darken1);
        }));

    public Task<byte[]> ExportAppointmentsPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken) =>
        Task.FromResult(Build(cancellationToken, "Appointment report (actuals)", column =>
        {
            Metric(column, "Total", data.AppointmentsInRange.ToString());
            Metric(column, "Completed", data.CompletedAppointments.ToString());
            Metric(column, "Cancelled", data.CancelledAppointments.ToString());
            Metric(column, "Expired/missed", data.ExpiredOrMissedAppointments.ToString());
            foreach (var row in data.AppointmentTrend.Take(40))
            {
                column.Item().Text($"{row.Label}: {row.Count}");
            }
        }));

    public Task<byte[]> ExportGrowthPdfAsync(ExecutiveAnalytics data, CancellationToken cancellationToken) =>
        Task.FromResult(Build(cancellationToken, "Patient growth report (actuals)", column =>
        {
            Metric(column, "New patients", data.NewPatientsInRange.ToString());
            Metric(column, "New doctors", data.NewDoctorsInRange.ToString());
            Metric(column, "Total patients", data.TotalPatients.ToString());
            Metric(column, "Verified doctors", data.VerifiedDoctors.ToString());
            Metric(column, "Retention %", data.PatientRetentionPercent.ToString("0.0"));
            Metric(column, "Repeat bookers", data.RepeatBookers.ToString());
        }));

    private static byte[] Build(CancellationToken cancellationToken, string title, Action<ColumnDescriptor> content)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.Header().Column(column =>
                {
                    column.Item().Text("ProDoc").FontSize(20).SemiBold().FontColor(Color.FromHex("#0F766E"));
                    column.Item().Text(title).FontSize(12);
                });
                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(4);
                    content(column);
                });
                page.Footer().AlignCenter().Text("Aggregated analytics export — no patient identifiers. Generated by ProDoc.").FontSize(8);
            });
        }).GeneratePdf();
    }

    private static void Metric(ColumnDescriptor column, string label, string value) =>
        column.Item().Text($"{label}: {value}");
}
