using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Analytics;

public sealed class AnalyticsService(
    IAnalyticsStore store,
    IAnalyticsReportExporter exporter,
    IMemoryCache cache,
    IClock clock) : IAnalyticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int MaxRangeDays = 366;

    public Task<ExecutiveAnalytics> GetExecutiveAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        return GetOrCreateAsync($"analytics:exec:{from:O}:{to:O}", () => BuildExecutiveAsync(from, to, cancellationToken), cancellationToken);
    }

    public Task<OperationalAnalytics> GetOperationalAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        return GetOrCreateAsync($"analytics:ops:{from:O}:{to:O}", () => BuildOperationalAsync(from, to, cancellationToken), cancellationToken);
    }

    public Task<PredictiveInsights> GetPredictiveAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync("analytics:predictive", () => BuildPredictiveAsync(cancellationToken), cancellationToken);

    public async Task<AnalyticsExportResult> ExportAsync(
        string reportType,
        string format,
        DateOnly from,
        DateOnly to,
        Guid actorUserId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        var kind = (reportType ?? string.Empty).Trim().ToLowerInvariant();
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        if (fmt is not ("csv" or "pdf"))
        {
            throw DomainException.Validation("export_format_invalid", "Export format must be csv or pdf.");
        }

        byte[] content;
        string contentType;
        string fileName;
        switch (kind)
        {
            case "executive":
            {
                var data = await GetExecutiveAsync(from, to, cancellationToken);
                if (fmt == "csv")
                {
                    content = Encoding.UTF8.GetBytes(BuildExecutiveCsv(data));
                    contentType = "text/csv";
                    fileName = $"prodoc-executive-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
                }
                else
                {
                    content = await exporter.ExportExecutivePdfAsync(data, cancellationToken);
                    contentType = "application/pdf";
                    fileName = $"prodoc-executive-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                }

                break;
            }
            case "operational":
            {
                var data = await GetOperationalAsync(from, to, cancellationToken);
                if (fmt == "csv")
                {
                    content = Encoding.UTF8.GetBytes(BuildOperationalCsv(data));
                    contentType = "text/csv";
                    fileName = $"prodoc-operational-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
                }
                else
                {
                    content = await exporter.ExportOperationalPdfAsync(data, cancellationToken);
                    contentType = "application/pdf";
                    fileName = $"prodoc-operational-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                }

                break;
            }
            case "financial":
            {
                var data = await GetExecutiveAsync(from, to, cancellationToken);
                if (fmt == "csv")
                {
                    content = Encoding.UTF8.GetBytes(BuildFinancialCsv(data));
                    contentType = "text/csv";
                    fileName = $"prodoc-financial-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
                }
                else
                {
                    content = await exporter.ExportFinancialPdfAsync(data, cancellationToken);
                    contentType = "application/pdf";
                    fileName = $"prodoc-financial-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                }

                break;
            }
            case "appointments":
            {
                var data = await GetExecutiveAsync(from, to, cancellationToken);
                if (fmt == "csv")
                {
                    content = Encoding.UTF8.GetBytes(BuildAppointmentsCsv(data));
                    contentType = "text/csv";
                    fileName = $"prodoc-appointments-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
                }
                else
                {
                    content = await exporter.ExportAppointmentsPdfAsync(data, cancellationToken);
                    contentType = "application/pdf";
                    fileName = $"prodoc-appointments-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                }

                break;
            }
            case "growth":
            {
                var data = await GetExecutiveAsync(from, to, cancellationToken);
                if (fmt == "csv")
                {
                    content = Encoding.UTF8.GetBytes(BuildGrowthCsv(data));
                    contentType = "text/csv";
                    fileName = $"prodoc-growth-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
                }
                else
                {
                    content = await exporter.ExportGrowthPdfAsync(data, cancellationToken);
                    contentType = "application/pdf";
                    fileName = $"prodoc-growth-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                }

                break;
            }
            default:
                throw DomainException.Validation(
                    "report_type_invalid",
                    "Supported report types: executive, operational, financial, appointments, growth.");
        }

        await store.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.AnalyticsExported, clock.UtcNow, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new AnalyticsExportResult(fileName, contentType, content);
    }

    private async Task<ExecutiveAnalytics> BuildExecutiveAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = UtcBounds(from, to);
        var patients = await store.CountPatientsAsync(cancellationToken);
        var doctors = await store.CountVerifiedDoctorsAsync(cancellationToken);
        var clinics = await store.CountClinicsAsync(cancellationToken);
        var newPatients = await store.CountNewPatientsAsync(fromUtc, toUtc, cancellationToken);
        var newDoctors = await store.CountNewDoctorsAsync(fromUtc, toUtc, cancellationToken);
        var facts = await store.ListAppointmentFactsByStartAsync(fromUtc, toUtc, cancellationToken);
        var finance = await store.SumFinanceAsync(fromUtc, toUtc, cancellationToken);
        var commissionDays = await store.CommissionByDayAsync(fromUtc, toUtc, cancellationToken);

        var total = facts.Count;
        var completed = facts.Count(item => item.Status == AppointmentStatus.Completed);
        var cancelled = facts.Count(item => item.Status == AppointmentStatus.Cancelled);
        var missed = facts.Count(item => item.Status == AppointmentStatus.Expired);
        var bookers = facts
            .Where(item => item.Status == AppointmentStatus.Completed)
            .GroupBy(item => item.AccountUserId)
            .Select(group => group.Count())
            .ToArray();
        var repeat = bookers.Count(count => count >= 2);
        var retentionDenom = bookers.Length;
        var retention = Rate(repeat, retentionDenom);

        var trend = facts
            .GroupBy(item => DateOnly.FromDateTime(item.StartsAtUtc.UtcDateTime))
            .OrderBy(group => group.Key)
            .Select(group => new AnalyticsNamedCount(group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), group.Count()))
            .ToArray();
        var commissionTrend = commissionDays
            .Select(item => new AnalyticsNamedAmount(item.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item.Commission))
            .ToArray();
        var topClinics = facts
            .Where(item => item.Status == AppointmentStatus.Completed)
            .GroupBy(item => item.ClinicName)
            .OrderByDescending(group => group.Count())
            .Take(8)
            .Select(group => new AnalyticsNamedCount(group.Key, group.Count()))
            .ToArray();
        var topDoctors = facts
            .Where(item => item.Status == AppointmentStatus.Completed)
            .GroupBy(item => item.DoctorName)
            .OrderByDescending(group => group.Count())
            .Take(8)
            .Select(group => new AnalyticsNamedCount(group.Key, group.Count()))
            .ToArray();

        return new ExecutiveAnalytics(
            from,
            to,
            "All appointment timing aggregates use UTC start times. Registration growth uses CreatedAtUtc.",
            patients,
            doctors,
            clinics,
            newPatients,
            newDoctors,
            total,
            completed,
            cancelled,
            missed,
            Rate(completed, total),
            Rate(cancelled, total),
            Rate(missed, total),
            finance.Gross,
            finance.Commission,
            finance.Refunds,
            finance.PendingPayouts,
            retention,
            repeat,
            trend,
            commissionTrend,
            topClinics,
            topDoctors,
            $"Cached up to {CacheTtl.TotalMinutes:0} minutes. Financial figures are actual captured commissions/refunds, not forecasts.");
    }

    private async Task<OperationalAnalytics> BuildOperationalAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = UtcBounds(from, to);
        var facts = await store.ListAppointmentFactsByStartAsync(fromUtc, toUtc, cancellationToken);
        var queue = await store.ListQueueEntriesAsync(from, to, cancellationToken);
        var waits = queue
            .Where(item => item.CalledAtUtc is not null)
            .Select(item => (item.CalledAtUtc!.Value - item.CheckInAtUtc).TotalMinutes)
            .ToArray();
        var consults = queue
            .Where(item => item.ConsultationStartedAtUtc is not null && item.ConsultationCompletedAtUtc is not null)
            .Select(item => (item.ConsultationCompletedAtUtc!.Value - item.ConsultationStartedAtUtc!.Value).TotalMinutes)
            .ToArray();

        var eligible = facts.Count(item => item.Status is not (AppointmentStatus.Held or AppointmentStatus.Rejected));
        var occupied = facts.Count(item =>
            item.Status is AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed);
        var walkIn = facts.Count(item => item.IsWalkIn);
        var online = facts.Count(item => !item.IsWalkIn);

        var peakHours = facts
            .GroupBy(item => item.StartsAtUtc.UtcDateTime.Hour)
            .OrderByDescending(group => group.Count())
            .Take(8)
            .Select(group => new AnalyticsNamedCount($"{group.Key:00}:00 UTC", group.Count()))
            .ToArray();
        var byCity = facts
            .GroupBy(item => string.IsNullOrWhiteSpace(item.City) ? "Unknown" : item.City!)
            .OrderByDescending(group => group.Count())
            .Take(10)
            .Select(group => new AnalyticsNamedCount(group.Key, group.Count()))
            .ToArray();

        var doctorSpecs = await store.ListDoctorSpecializationsAsync(cancellationToken);
        var primarySpec = doctorSpecs
            .GroupBy(item => item.DoctorId)
            .ToDictionary(group => group.Key, group => group.First().SpecializationName);
        var bySpec = facts
            .GroupBy(item => primarySpec.TryGetValue(item.DoctorId, out var name) ? name : "Unspecified")
            .OrderByDescending(group => group.Count())
            .Take(10)
            .Select(group => new AnalyticsNamedCount(group.Key, group.Count()))
            .ToArray();

        var days = Math.Max(1, to.DayNumber - from.DayNumber + 1);
        var doctorUtil = facts
            .GroupBy(item => new { item.DoctorId, item.DoctorName })
            .Select(group =>
            {
                var scheduled = group.Count(item =>
                    item.Status is AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed
                        or AppointmentStatus.Cancelled or AppointmentStatus.Expired);
                var completed = group.Count(item => item.Status == AppointmentStatus.Completed);
                return new DoctorUtilizationRow(
                    group.Key.DoctorId,
                    group.Key.DoctorName,
                    completed,
                    scheduled,
                    Rate(completed, scheduled));
            })
            .OrderByDescending(item => item.CompletedVisits)
            .Take(20)
            .ToArray();

        var branchWait = queue
            .Where(item => item.CalledAtUtc is not null)
            .GroupBy(item => item.BranchId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(item => (item.CalledAtUtc!.Value - item.CheckInAtUtc).TotalMinutes));

        var branches = facts
            .GroupBy(item => new { item.BranchId, item.ClinicName, item.BranchName, item.City })
            .Select(group => new BranchPerformanceRow(
                group.Key.BranchId,
                group.Key.ClinicName,
                group.Key.BranchName,
                group.Key.City,
                group.Count(item => item.Status == AppointmentStatus.Completed),
                group.Count(item => item.Status == AppointmentStatus.Cancelled),
                branchWait.TryGetValue(group.Key.BranchId, out var wait) ? wait : null))
            .OrderByDescending(item => item.Completions)
            .Take(20)
            .ToArray();

        _ = days;
        return new OperationalAnalytics(
            from,
            to,
            waits.Length == 0 ? null : waits.Average(),
            consults.Length == 0 ? null : consults.Average(),
            Rate(occupied, eligible),
            walkIn,
            online,
            peakHours,
            byCity,
            bySpec,
            doctorUtil,
            branches,
            "Wait/consult durations come from queue timestamps only. Slot utilization is confirmed/checked-in/completed vs non-hold appointments. Peak hours are UTC. No patient identifiers are included.");
    }

    private async Task<PredictiveInsights> BuildPredictiveAsync(CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var historyFrom = asOf.AddDays(-56);
        var (fromUtc, toUtc) = UtcBounds(historyFrom, asOf);
        var facts = await store.ListAppointmentFactsByStartAsync(fromUtc, toUtc, cancellationToken);
        var daily = facts
            .GroupBy(item => DateOnly.FromDateTime(item.StartsAtUtc.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var demand = new List<ForecastPoint>();
        for (var i = 1; i <= 7; i++)
        {
            var day = asOf.AddDays(i);
            var samples = new List<int>();
            for (var w = 1; w <= 4; w++)
            {
                var prior = day.AddDays(-7 * w);
                if (daily.TryGetValue(prior, out var count))
                {
                    samples.Add(count);
                }
            }

            var predicted = samples.Count == 0 ? 0 : samples.Average();
            var confidence = samples.Count switch
            {
                0 => 0,
                1 => 35,
                2 => 50,
                3 => 65,
                _ => 75
            };
            demand.Add(new ForecastPoint(
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Math.Round(predicted, 1),
                confidence,
                samples.Count == 0
                    ? "Insufficient same-weekday history"
                    : $"Mean of {samples.Count} prior same weekdays"));
        }

        var clinicVolume = facts
            .GroupBy(item => item.ClinicName)
            .Select(group =>
            {
                var last14 = group.Count(item => DateOnly.FromDateTime(item.StartsAtUtc.UtcDateTime) >= asOf.AddDays(-14));
                var predicted = last14 / 2.0;
                var confidence = last14 < 5 ? 30 : last14 < 20 ? 55 : 70;
                return new ForecastPoint(group.Key, Math.Round(predicted, 1), confidence, "Half of last-14-day volume as next-7-day estimate");
            })
            .OrderByDescending(item => item.PredictedValue)
            .Take(8)
            .ToArray();

        var risk = facts
            .GroupBy(item => item.StartsAtUtc.UtcDateTime.Hour)
            .Select(group =>
            {
                var denom = group.Count(item =>
                    item.Status is AppointmentStatus.Confirmed or AppointmentStatus.Completed or AppointmentStatus.Cancelled
                        or AppointmentStatus.Expired or AppointmentStatus.CheckedIn);
                var missed = group.Count(item => item.Status == AppointmentStatus.Expired);
                return new AnalyticsNamedRate($"{group.Key:00}:00 UTC", Rate(missed, denom), missed, denom);
            })
            .Where(item => item.Denominator >= 3)
            .OrderByDescending(item => item.RatePercent)
            .Take(8)
            .ToArray();

        var util = await BuildOperationalAsync(asOf.AddDays(-14), asOf, cancellationToken);
        var hints = util.DoctorUtilization
            .Where(item => item.ScheduledVisits >= 3)
            .Select(item =>
            {
                if (item.UtilizationPercent >= 80)
                {
                    return new AnalyticsNamedCount($"{item.DoctorName}: high load — consider opening slots", (int)item.UtilizationPercent);
                }

                if (item.UtilizationPercent <= 25)
                {
                    return new AnalyticsNamedCount($"{item.DoctorName}: spare capacity — promote availability", (int)item.UtilizationPercent);
                }

                return null;
            })
            .Where(item => item is not null)
            .Cast<AnalyticsNamedCount>()
            .Take(10)
            .ToArray();

        return new PredictiveInsights(
            asOf,
            demand,
            clinicVolume,
            risk,
            hints,
            "FORECAST ONLY — not actual results. Simple weekday averages and moving volumes; not clinical decision support. Never used to deny appointments. Confidence reflects sample size, not statistical guarantees.");
    }

    private async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();
        cancellationToken.ThrowIfCancellationRequested();
        cache.Set(key, value, CacheTtl);
        return value;
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw DomainException.Validation("date_invalid", "End date must be on or after start date.");
        }

        if (to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw DomainException.Validation("date_range_too_large", "Choose a range of 366 days or fewer.");
        }
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) UtcBounds(DateOnly from, DateOnly to) =>
        (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero));

    private static double Rate(int numerator, int denominator) =>
        denominator <= 0 ? 0 : Math.Round(100.0 * numerator / denominator, 1);

    private static string BuildExecutiveCsv(ExecutiveAnalytics data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("metric,value,note");
        sb.AppendLine(Csv("total_patients", data.TotalPatients, "count"));
        sb.AppendLine(Csv("verified_doctors", data.VerifiedDoctors, "count"));
        sb.AppendLine(Csv("total_clinics", data.TotalClinics, "count"));
        sb.AppendLine(Csv("new_patients", data.NewPatientsInRange, "registrations in range"));
        sb.AppendLine(Csv("new_doctors", data.NewDoctorsInRange, "registrations in range"));
        sb.AppendLine(Csv("appointments", data.AppointmentsInRange, "by start UTC"));
        sb.AppendLine(Csv("completed", data.CompletedAppointments, "actual"));
        sb.AppendLine(Csv("cancelled", data.CancelledAppointments, "actual"));
        sb.AppendLine(Csv("expired_or_missed", data.ExpiredOrMissedAppointments, "Expired status proxy for missed"));
        sb.AppendLine(Csv("completion_rate_percent", data.CompletionRatePercent, "actual"));
        sb.AppendLine(Csv("gross_booking_value", data.GrossBookingValue, "non-walk-in fees in range"));
        sb.AppendLine(Csv("commission_revenue", data.CommissionRevenue, "captured snapshots"));
        sb.AppendLine(Csv("refunds", data.Refunds, "completed refunds"));
        sb.AppendLine(Csv("pending_payouts", data.PendingPayouts, "unsettled clinic payable"));
        sb.AppendLine(Csv("patient_retention_percent", data.PatientRetentionPercent, "repeat completers / completers"));
        return sb.ToString();
    }

    private static string BuildOperationalCsv(OperationalAnalytics data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("section,label,value");
        sb.AppendLine(Csv("summary", "average_wait_minutes", data.AverageWaitMinutes?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""));
        sb.AppendLine(Csv("summary", "average_consultation_minutes", data.AverageConsultationMinutes?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""));
        sb.AppendLine(Csv("summary", "slot_utilization_percent", data.SlotUtilizationPercent));
        sb.AppendLine(Csv("summary", "walk_in", data.WalkInBookings));
        sb.AppendLine(Csv("summary", "online", data.OnlineBookings));
        foreach (var row in data.PeakBookingHoursUtc)
        {
            sb.AppendLine(Csv("peak_hour", row.Label, row.Count));
        }

        foreach (var row in data.DemandByCity)
        {
            sb.AppendLine(Csv("demand_city", row.Label, row.Count));
        }

        foreach (var row in data.DemandBySpecialization)
        {
            sb.AppendLine(Csv("demand_specialization", row.Label, row.Count));
        }

        foreach (var row in data.DoctorUtilization)
        {
            sb.AppendLine(Csv("doctor_utilization", row.DoctorName, $"{row.CompletedVisits}/{row.ScheduledVisits} ({row.UtilizationPercent}%)"));
        }

        foreach (var row in data.BranchPerformance)
        {
            sb.AppendLine(Csv("branch", $"{row.ClinicName}/{row.BranchName}", $"completions={row.Completions};cancellations={row.Cancellations}"));
        }

        return sb.ToString();
    }

    private static string BuildFinancialCsv(ExecutiveAnalytics data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("metric,amount_inr,basis");
        sb.AppendLine(Csv("gross_booking_value", data.GrossBookingValue, "actual non-walk-in consultation fees"));
        sb.AppendLine(Csv("commission_revenue", data.CommissionRevenue, "actual commission snapshots"));
        sb.AppendLine(Csv("refunds", data.Refunds, "completed refunds"));
        sb.AppendLine(Csv("pending_payouts", data.PendingPayouts, "unsettled clinic payable"));
        foreach (var row in data.CommissionTrend)
        {
            sb.AppendLine(Csv("commission_by_day", row.Amount, row.Label));
        }

        return sb.ToString();
    }

    private static string BuildAppointmentsCsv(ExecutiveAnalytics data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("date,appointments");
        foreach (var row in data.AppointmentTrend)
        {
            sb.AppendLine($"{Escape(row.Label)},{row.Count}");
        }

        return sb.ToString();
    }

    private static string BuildGrowthCsv(ExecutiveAnalytics data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("metric,value");
        sb.AppendLine(Csv("new_patients", data.NewPatientsInRange, ""));
        sb.AppendLine(Csv("new_doctors", data.NewDoctorsInRange, ""));
        sb.AppendLine(Csv("total_patients", data.TotalPatients, ""));
        sb.AppendLine(Csv("verified_doctors", data.VerifiedDoctors, ""));
        sb.AppendLine(Csv("patient_retention_percent", data.PatientRetentionPercent, ""));
        sb.AppendLine(Csv("repeat_bookers", data.RepeatBookers, ""));
        return sb.ToString();
    }

    private static string Csv(string a, object b, object c) =>
        $"{Escape(a)},{Escape(Convert.ToString(b, CultureInfo.InvariantCulture) ?? "")},{Escape(Convert.ToString(c, CultureInfo.InvariantCulture) ?? "")}";

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
