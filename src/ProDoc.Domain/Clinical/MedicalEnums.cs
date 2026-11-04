namespace ProDoc.Domain.Clinical;

public enum ConsultationStatus
{
    InProgress = 1,
    Completed = 2
}

public enum PrescriptionStatus
{
    Draft = 1,
    Finalized = 2,
    Amended = 3,
    Voided = 4
}

public enum AllergySeverity
{
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Unknown = 4
}

public enum MedicalAccessAction
{
    RecordViewed = 1,
    ConsultationViewed = 2,
    ConsultationCreated = 3,
    ConsultationUpdated = 4,
    PrescriptionViewed = 5,
    PrescriptionDrafted = 6,
    PrescriptionFinalized = 7,
    PrescriptionAmended = 8,
    PrescriptionPdfDownloaded = 9,
    DocumentUploaded = 10,
    DocumentDownloaded = 11,
    DocumentViewed = 12,
    AllergyChanged = 13,
    ConditionChanged = 14,
    MedicationChanged = 15
}
