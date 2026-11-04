namespace ProDoc.Application.Admin;

public sealed record AdminDashboardMetrics(
    int TotalPatients,
    int VerifiedDoctors,
    int TotalClinics,
    int TotalBranches,
    int PendingDoctorApplications,
    int PendingClinicApprovals,
    int TotalAppointments,
    int CompletedAppointments,
    int CancelledAppointments,
    int ActiveQueueEntries,
    decimal GrossBookingValue,
    decimal CommissionRevenue,
    decimal PendingSettlements,
    decimal Refunds,
    IReadOnlyList<NamedCount> MonthlyAppointments,
    IReadOnlyList<NamedAmount> MonthlyRevenue,
    IReadOnlyList<NamedCount> PatientGrowth,
    IReadOnlyList<NamedCount> ProviderGrowth);

public sealed record NamedCount(string Label, int Count);

public sealed record NamedAmount(string Label, decimal Amount);

public sealed record AdminUserSummary(
    Guid UserId,
    string PhoneNumber,
    string? FullName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? DeletionRequestedAtUtc,
    Guid? DoctorId,
    string? DoctorStatus,
    Guid? OwnedClinicId,
    string? OwnedClinicStatus);

public sealed record AuditEntryDetails(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    DateTimeOffset OccurredAtUtc,
    string? IpAddress);

public sealed record SupportTicketSummary(
    Guid Id,
    string Number,
    string Category,
    string Priority,
    string Status,
    string Subject,
    Guid CreatedByUserId,
    Guid? AssignedToUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DueAtUtc = null,
    DateTimeOffset? EscalatedAtUtc = null,
    bool IsOverdue = false);

public sealed record SupportTicketNoteDetails(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    bool IsInternal,
    DateTimeOffset CreatedAtUtc);

public sealed record SupportTicketDetails(
    Guid Id,
    string Number,
    string Category,
    string Priority,
    string Status,
    string Subject,
    string Description,
    Guid CreatedByUserId,
    Guid? AssignedToUserId,
    Guid? AppointmentId,
    Guid? PaymentId,
    Guid? ClinicId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    IReadOnlyList<SupportTicketNoteDetails> Notes,
    DateTimeOffset? ClosedAtUtc = null,
    DateTimeOffset? EscalatedAtUtc = null,
    DateTimeOffset? DueAtUtc = null,
    string? ResolutionDetails = null,
    IReadOnlyList<ProDoc.Application.Support.SupportAttachmentDetails>? Attachments = null,
    IReadOnlyList<ProDoc.Application.Support.TicketStatusHistoryDetails>? StatusHistory = null);

public sealed record PlatformSettingDetails(Guid Id, string Key, string Value, string? Description, DateTimeOffset UpdatedAtUtc);

public sealed record FeatureFlagDetails(Guid Id, string Key, bool Enabled, string? Description, DateTimeOffset UpdatedAtUtc);

public sealed record ProviderDirectoryItem(
    Guid Id,
    string Kind,
    string Name,
    string Status,
    string? City,
    string? State,
    DateTimeOffset UpdatedAtUtc);

public sealed record FinanceReportRow(
    Guid AppointmentId,
    Guid ClinicId,
    string? ClinicName,
    string? City,
    string? State,
    decimal ConsultationFee,
    decimal CommissionAmount,
    decimal ClinicPayable,
    DateTimeOffset CapturedAtUtc,
    bool IsWalkInExcluded);
