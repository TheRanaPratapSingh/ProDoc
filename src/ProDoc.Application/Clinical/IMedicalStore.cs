using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Clinical;

public interface IMedicalStore
{
    Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<ClinicBranch?> FindBranchAsync(Guid branchId, CancellationToken cancellationToken);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken);

    Task<bool> DoctorHasCareRelationshipAsync(
        Guid doctorId,
        Guid accountUserId,
        Guid? familyMemberId,
        CancellationToken cancellationToken);

    Task<PatientMedicalRecord?> FindRecordAsync(
        Guid accountUserId,
        Guid? familyMemberId,
        bool tracking,
        CancellationToken cancellationToken);

    Task<PatientMedicalRecord?> FindRecordByIdAsync(Guid recordId, bool tracking, CancellationToken cancellationToken);

    void AddRecord(PatientMedicalRecord record);

    Task<ConsultationRecord?> FindConsultationAsync(Guid consultationId, bool tracking, CancellationToken cancellationToken);

    Task<ConsultationRecord?> FindActiveConsultationForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationRecord>> ListConsultationsAsync(Guid medicalRecordId, CancellationToken cancellationToken);

    void AddConsultation(ConsultationRecord consultation);

    Task DeleteDiagnosesForConsultationAsync(Guid consultationId, CancellationToken cancellationToken);

    void AddDiagnosis(ConsultationDiagnosis diagnosis);

    Task DeleteItemsForVersionAsync(Guid versionId, CancellationToken cancellationToken);

    void AddPrescriptionItem(PrescriptionItem item);

    Task<Prescription?> FindPrescriptionAsync(Guid prescriptionId, bool tracking, CancellationToken cancellationToken);

    Task<Prescription?> FindPrescriptionForMutationAsync(Guid prescriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PrescriptionItem>> ListVersionItemsAsync(Guid versionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Prescription>> ListPrescriptionsAsync(Guid medicalRecordId, CancellationToken cancellationToken);

    void AddPrescription(Prescription prescription);

    void AddPrescriptionVersion(PrescriptionVersion version);

    Task<string> NextPrescriptionNumberAsync(CancellationToken cancellationToken);

    Task<PatientAllergy?> FindAllergyAsync(Guid allergyId, bool tracking, CancellationToken cancellationToken);

    void AddAllergy(PatientAllergy allergy);

    Task<PatientMedicalCondition?> FindConditionAsync(Guid conditionId, bool tracking, CancellationToken cancellationToken);

    void AddCondition(PatientMedicalCondition condition);

    Task<PatientMedication?> FindMedicationAsync(Guid medicationId, bool tracking, CancellationToken cancellationToken);

    void AddMedication(PatientMedication medication);

    Task<MedicalDocument?> FindDocumentAsync(Guid documentId, bool tracking, CancellationToken cancellationToken);

    void AddDocument(MedicalDocument document);

    Task<IReadOnlyList<MedicineCatalogEntry>> SearchMedicinesAsync(string query, int take, CancellationToken cancellationToken);

    Task<MedicineCatalogEntry?> FindCatalogEntryAsync(Guid id, CancellationToken cancellationToken);

    void AddCatalogEntry(MedicineCatalogEntry entry);

    void AddAccessLog(MedicalRecordAccessLog log);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> DoctorNamesAsync(IEnumerable<Guid> doctorIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> ClinicNamesAsync(IEnumerable<Guid> clinicIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> BranchNamesAsync(IEnumerable<Guid> branchIds, CancellationToken cancellationToken);

    Task<FamilyMember?> FindFamilyMemberAsync(Guid familyMemberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientMedicalRecord>> ListRecordsForAccountAsync(Guid accountUserId, CancellationToken cancellationToken);

    Task<(int InProgress, int CompletedToday, int WithPrescriptionToday)> CountDoctorActivityAsync(
        Guid doctorId,
        DateOnly today,
        Guid? clinicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProDoc.Application.Providers.DoctorPrescriptionRow>> ListDoctorPrescriptionsAsync(
        Guid doctorId,
        Guid? clinicId,
        CancellationToken cancellationToken);
}

public interface IPrescriptionPdfGenerator
{
    Task<byte[]> GenerateAsync(Prescription prescription, PrescriptionVersion version, CancellationToken cancellationToken);
}
