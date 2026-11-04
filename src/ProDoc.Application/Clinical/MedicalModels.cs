namespace ProDoc.Application.Clinical;

public sealed record DiagnosisInput(string Description, string? Code, bool IsPrimary);

public sealed record MedicineInput(
    Guid? CatalogEntryId,
    string MedicineName,
    string Dosage,
    string Frequency,
    string Duration,
    string Route,
    string? Instructions);

public sealed record DiagnosisDetails(Guid Id, string Description, string? Code, bool IsPrimary, int SortOrder);

public sealed record MedicineDetails(
    Guid Id,
    Guid? CatalogEntryId,
    string MedicineName,
    string Dosage,
    string Frequency,
    string Duration,
    string Route,
    string? Instructions,
    int SortOrder);

public sealed record MedicineCatalogItem(Guid Id, string Name, string? Form, string? StrengthHint);

public sealed record AllergyDetails(
    Guid Id,
    string Substance,
    string? Reaction,
    string Severity,
    bool IsActive,
    DateTimeOffset RecordedAtUtc);

public sealed record ConditionDetails(
    Guid Id,
    string Name,
    string? Notes,
    DateOnly? OnsetDate,
    bool IsActive,
    DateTimeOffset RecordedAtUtc);

public sealed record MedicationDetails(
    Guid Id,
    string Name,
    string? Dosage,
    string? Frequency,
    bool IsCurrent,
    DateOnly? StartedOn,
    DateOnly? EndedOn,
    string? Notes,
    DateTimeOffset RecordedAtUtc);

public sealed record MedicalDocumentDetails(
    Guid Id,
    string Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Notes,
    DateTimeOffset UploadedAtUtc);

public sealed record ConsultationSummary(
    Guid Id,
    Guid MedicalRecordId,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    Guid? AppointmentId,
    string PatientName,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<string> Diagnoses);

public sealed record ConsultationDetails(
    Guid Id,
    Guid MedicalRecordId,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    Guid? AppointmentId,
    Guid AccountUserId,
    Guid? FamilyMemberId,
    string PatientName,
    string? Symptoms,
    string? ClinicalObservations,
    string? DoctorNotes,
    string? Advice,
    DateOnly? FollowUpDate,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<DiagnosisDetails> Diagnoses,
    IReadOnlyList<PrescriptionSummary> Prescriptions);

public sealed record PrescriptionSummary(
    Guid Id,
    string Number,
    string Status,
    int CurrentVersionNumber,
    DateTimeOffset UpdatedAtUtc,
    string? Diagnosis,
    DateTimeOffset? FinalizedAtUtc);

public sealed record PrescriptionVersionDetails(
    Guid Id,
    int VersionNumber,
    string Status,
    string? Diagnosis,
    string? Symptoms,
    string? ClinicalObservations,
    string? Advice,
    DateOnly? FollowUpDate,
    string PatientName,
    string DoctorName,
    string? DoctorRegistration,
    string ClinicName,
    string BranchName,
    string? BranchAddress,
    DateTimeOffset ConsultationDateUtc,
    Guid? AmendedFromVersionId,
    string? AmendmentReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinalizedAtUtc,
    bool HasPdf,
    IReadOnlyList<MedicineDetails> Medicines);

public sealed record PrescriptionDetails(
    Guid Id,
    string Number,
    Guid MedicalRecordId,
    Guid? ConsultationRecordId,
    Guid DoctorId,
    Guid ClinicId,
    Guid BranchId,
    Guid AccountUserId,
    Guid? FamilyMemberId,
    string Status,
    int CurrentVersionNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PrescriptionVersionDetails Current,
    IReadOnlyList<PrescriptionVersionDetails> Versions);

public sealed record MedicalRecordSummary(
    Guid Id,
    Guid AccountUserId,
    Guid? FamilyMemberId,
    string SubjectName,
    DateTimeOffset UpdatedAtUtc,
    int ConsultationCount,
    int PrescriptionCount,
    int DocumentCount);

public sealed record MedicalRecordDetails(
    Guid Id,
    Guid AccountUserId,
    Guid? FamilyMemberId,
    string SubjectName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AllergyDetails> Allergies,
    IReadOnlyList<ConditionDetails> Conditions,
    IReadOnlyList<MedicationDetails> Medications,
    IReadOnlyList<ConsultationSummary> Consultations,
    IReadOnlyList<PrescriptionSummary> Prescriptions,
    IReadOnlyList<MedicalDocumentDetails> Documents);

public sealed record WorkspaceDetails(
    Guid? AppointmentId,
    Guid DoctorId,
    Guid ClinicId,
    Guid BranchId,
    Guid AccountUserId,
    Guid? FamilyMemberId,
    string PatientName,
    string AppointmentStatus,
    DateTimeOffset? AppointmentStartsAtUtc,
    string? AppointmentReason,
    MedicalRecordDetails Record,
    ConsultationDetails? ActiveConsultation);

public sealed record StoredPdf(Stream Content, string FileName, string ContentType);
