using ProDoc.Application.Clinical;

namespace ProDoc.Api.Contracts;

public sealed record UpdateConsultationRequest(
    string? Symptoms,
    string? ClinicalObservations,
    string? DoctorNotes,
    string? Advice,
    DateOnly? FollowUpDate,
    IReadOnlyList<DiagnosisInput>? Diagnoses,
    bool Complete);

public sealed record UpdatePrescriptionRequest(
    string? Diagnosis,
    string? Symptoms,
    string? ClinicalObservations,
    string? Advice,
    DateOnly? FollowUpDate,
    IReadOnlyList<MedicineInput>? Medicines);

public sealed record AmendPrescriptionRequest(string Reason);

public sealed record UpsertAllergyRequest(
    Guid? AllergyId,
    string Substance,
    string? Reaction,
    string Severity,
    bool IsActive = true);

public sealed record UpsertConditionRequest(
    Guid? ConditionId,
    string Name,
    string? Notes,
    DateOnly? OnsetDate,
    bool IsActive = true);

public sealed record UpsertMedicationRequest(
    Guid? MedicationId,
    string Name,
    string? Dosage,
    string? Frequency,
    bool IsCurrent = true,
    DateOnly? StartedOn = null,
    DateOnly? EndedOn = null,
    string? Notes = null);

public sealed record AddMedicineCatalogRequest(string Name, string? Form, string? StrengthHint);
