namespace ProDoc.Application.Assistant;

public sealed record AssistantConsentDetails(
    bool RecordsAccessAllowed,
    string ConsentVersion,
    DateTimeOffset? RecordsConsentAtUtc,
    string Disclaimer,
    string EmergencyGuidance);

public sealed record AssistantConversationSummary(
    Guid Id,
    string Title,
    string Language,
    DateTimeOffset UpdatedAtUtc,
    bool HasPendingAction);

public sealed record AssistantSourceRef(string Kind, string Label, string? Id);

public sealed record AssistantDoctorRecommendation(
    Guid DoctorId,
    Guid ScheduleId,
    string DisplayName,
    string Specialization,
    string? ClinicName,
    string? City,
    decimal ConsultationFee,
    int YearsOfExperience,
    IReadOnlyList<string> Languages,
    double? DistanceKm,
    string Explanation,
    string? RatingNote);

public sealed record AssistantPendingActionDetails(
    Guid ActionId,
    string Type,
    string Summary,
    Guid? ScheduleId,
    Guid? AppointmentId,
    string? Date,
    string? Start,
    string? DoctorName);

public sealed record AssistantMessageDetails(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<AssistantSourceRef> Sources,
    DateTimeOffset CreatedAtUtc,
    string? ToolName);

public sealed record AssistantConversationDetails(
    Guid Id,
    string Title,
    string Language,
    string PromptVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    AssistantPendingActionDetails? PendingAction,
    IReadOnlyList<AssistantMessageDetails> Messages,
    IReadOnlyList<AssistantDoctorRecommendation> Recommendations);

public sealed record AssistantTurnResult(
    Guid ConversationId,
    AssistantMessageDetails AssistantMessage,
    AssistantPendingActionDetails? PendingAction,
    IReadOnlyList<AssistantDoctorRecommendation> Recommendations,
    bool IsEmergency,
    string Disclaimer);

public sealed record ConfirmAssistantActionResult(
    Guid ConversationId,
    string ActionType,
    Guid? AppointmentId,
    string Status,
    string Message);
