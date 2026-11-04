namespace ProDoc.Application.Analytics;

public sealed record AnalyticsNamedCount(string Label, int Count);

public sealed record AnalyticsNamedAmount(string Label, decimal Amount);

public sealed record AnalyticsNamedRate(string Label, double RatePercent, int Numerator, int Denominator);

public sealed record ExecutiveAnalytics(
    DateOnly From,
    DateOnly To,
    string TimeZoneNote,
    int TotalPatients,
    int VerifiedDoctors,
    int TotalClinics,
    int NewPatientsInRange,
    int NewDoctorsInRange,
    int AppointmentsInRange,
    int CompletedAppointments,
    int CancelledAppointments,
    int ExpiredOrMissedAppointments,
    double CompletionRatePercent,
    double CancellationRatePercent,
    double MissedRatePercent,
    decimal GrossBookingValue,
    decimal CommissionRevenue,
    decimal Refunds,
    decimal PendingPayouts,
    double PatientRetentionPercent,
    int RepeatBookers,
    IReadOnlyList<AnalyticsNamedCount> AppointmentTrend,
    IReadOnlyList<AnalyticsNamedAmount> CommissionTrend,
    IReadOnlyList<AnalyticsNamedCount> TopClinicsByCompletions,
    IReadOnlyList<AnalyticsNamedCount> TopDoctorsByCompletions,
    string DataFreshnessNote);

public sealed record OperationalAnalytics(
    DateOnly From,
    DateOnly To,
    double? AverageWaitMinutes,
    double? AverageConsultationMinutes,
    double SlotUtilizationPercent,
    int WalkInBookings,
    int OnlineBookings,
    IReadOnlyList<AnalyticsNamedCount> PeakBookingHoursUtc,
    IReadOnlyList<AnalyticsNamedCount> DemandByCity,
    IReadOnlyList<AnalyticsNamedCount> DemandBySpecialization,
    IReadOnlyList<DoctorUtilizationRow> DoctorUtilization,
    IReadOnlyList<BranchPerformanceRow> BranchPerformance,
    string MethodologyNote);

public sealed record DoctorUtilizationRow(
    Guid DoctorId,
    string DoctorName,
    int CompletedVisits,
    int ScheduledVisits,
    double UtilizationPercent);

public sealed record BranchPerformanceRow(
    Guid BranchId,
    string ClinicName,
    string BranchName,
    string? City,
    int Completions,
    int Cancellations,
    double? AverageWaitMinutes);

public sealed record ForecastPoint(string Label, double PredictedValue, double ConfidencePercent, string Basis);

public sealed record PredictiveInsights(
    DateOnly AsOf,
    IReadOnlyList<ForecastPoint> DemandForecastNext7Days,
    IReadOnlyList<ForecastPoint> ExpectedVolumeByClinic,
    IReadOnlyList<AnalyticsNamedRate> NoShowRiskByHourUtc,
    IReadOnlyList<AnalyticsNamedCount> AvailabilityPlanningHints,
    string Disclaimer);

public sealed record AnalyticsExportResult(string FileName, string ContentType, byte[] Content);
