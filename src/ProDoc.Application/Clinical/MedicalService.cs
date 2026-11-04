using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Automation;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Clinical;

public sealed class MedicalService(
    IMedicalStore store,
    IDocumentStorage documents,
    IUploadScanner scanner,
    IPrescriptionPdfGenerator pdfs,
    IAutomationEventBus automation,
    IClock clock,
    ILogger<MedicalService> logger)
{
    public async Task<WorkspaceDetails> OpenWorkspaceAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        if (appointment.Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.Completed or AppointmentStatus.Confirmed))
        {
            throw DomainException.Conflict("consultation_not_ready", "Open a checked-in or completed visit for consultation.");
        }

        var record = await EnsureRecordAsync(appointment.AccountUserId, appointment.FamilyMemberId, appointment.PatientName, cancellationToken);
        await AccessAsync(userId, record.Id, MedicalAccessAction.RecordViewed, metadata, cancellationToken);
        var active = await store.FindActiveConsultationForAppointmentAsync(appointmentId, cancellationToken);
        ConsultationDetails? activeDetails = active is null ? null : await MapConsultationAsync(active, cancellationToken);
        return new WorkspaceDetails(
            appointment.Id,
            appointment.DoctorId,
            appointment.ClinicId,
            appointment.BranchId,
            appointment.AccountUserId,
            appointment.FamilyMemberId,
            appointment.PatientName,
            appointment.Status.ToString(),
            appointment.StartsAtUtc,
            appointment.Reason,
            await MapRecordAsync(record, cancellationToken),
            activeDetails);
    }

    public async Task<ConsultationDetails> StartConsultationAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        var existing = await store.FindActiveConsultationForAppointmentAsync(appointmentId, cancellationToken);
        if (existing is not null)
        {
            return await MapConsultationAsync(existing, cancellationToken);
        }

        var record = await EnsureRecordAsync(appointment.AccountUserId, appointment.FamilyMemberId, appointment.PatientName, cancellationToken);
        var consultation = ConsultationRecord.Start(
            record.Id,
            doctor.Id,
            appointment.ClinicId,
            appointment.BranchId,
            appointment.AccountUserId,
            appointment.FamilyMemberId,
            appointment.PatientName,
            appointment.Id,
            null,
            clock.UtcNow);
        store.AddConsultation(consultation);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, record.Id, MedicalAccessAction.ConsultationCreated, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, consultation.Id));
        await store.AddAuditAsync(Audit(userId, AuditActions.ConsultationCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapConsultationAsync(consultation, cancellationToken);
    }

    public async Task<ConsultationDetails> UpdateConsultationAsync(
        Guid userId,
        Guid consultationId,
        string? symptoms,
        string? clinicalObservations,
        string? doctorNotes,
        string? advice,
        DateOnly? followUpDate,
        IReadOnlyList<DiagnosisInput> diagnoses,
        bool complete,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var consultation = await store.FindConsultationAsync(consultationId, true, cancellationToken)
            ?? throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        if (consultation.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        }

        await store.DeleteDiagnosesForConsultationAsync(consultation.Id, cancellationToken);
        consultation.UpdateNotes(symptoms, clinicalObservations, doctorNotes, advice, followUpDate, clock.UtcNow);
        var order = 0;
        foreach (var item in diagnoses)
        {
            store.AddDiagnosis(ConsultationDiagnosis.Create(consultation.Id, item.Description, item.Code, item.IsPrimary, order++));
        }

        if (complete)
        {
            consultation.Complete(clock.UtcNow, diagnoses.Count > 0);
        }

        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId,
            consultation.MedicalRecordId,
            complete ? MedicalAccessAction.ConsultationUpdated : MedicalAccessAction.ConsultationUpdated,
            clock.UtcNow,
            metadata.IpAddress,
            metadata.UserAgent,
            consultation.Id));
        await store.AddAuditAsync(
            Audit(userId, complete ? AuditActions.ConsultationCompleted : AuditActions.ConsultationUpdated, metadata),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapConsultationAsync(consultation, cancellationToken);
    }

    public async Task<ConsultationDetails> GetConsultationAsync(
        Guid userId,
        Guid consultationId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var consultation = await store.FindConsultationAsync(consultationId, false, cancellationToken)
            ?? throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        await EnsureCanReadRecordAsync(userId, consultation.MedicalRecordId, consultation.AccountUserId, consultation.FamilyMemberId, consultation.DoctorId, cancellationToken);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, consultation.MedicalRecordId, MedicalAccessAction.ConsultationViewed, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, consultation.Id));
        await store.SaveChangesAsync(cancellationToken);
        return await MapConsultationAsync(consultation, cancellationToken);
    }

    public async Task<PrescriptionDetails> CreatePrescriptionDraftAsync(
        Guid userId,
        Guid consultationId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var consultation = await store.FindConsultationAsync(consultationId, false, cancellationToken)
            ?? throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        if (consultation.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("consultation_not_found", "That consultation was not found.");
        }

        var clinic = await store.FindClinicAsync(consultation.ClinicId, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        var branch = await store.FindBranchAsync(consultation.BranchId, cancellationToken)
            ?? throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        var number = await store.NextPrescriptionNumberAsync(cancellationToken);
        var registration = doctor.Registrations
            .OrderByDescending(item => item.ExpiresOn)
            .Select(item => $"{item.Number} ({item.Council})")
            .FirstOrDefault();
        var prescription = Prescription.CreateDraft(
            number,
            consultation.MedicalRecordId,
            doctor.Id,
            consultation.ClinicId,
            consultation.BranchId,
            consultation.AccountUserId,
            consultation.FamilyMemberId,
            userId,
            consultation.PatientName,
            doctor.DisplayName ?? $"Dr. {doctor.FirstName} {doctor.LastName}".Trim(),
            registration,
            clinic.Name ?? "Clinic",
            branch.Name ?? "Branch",
            FormatAddress(branch),
            consultation.StartedAtUtc,
            clock.UtcNow,
            consultation.Id,
            consultation.AppointmentId);
        store.AddPrescription(prescription);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, consultation.MedicalRecordId, MedicalAccessAction.PrescriptionDrafted, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.AddAuditAsync(Audit(userId, AuditActions.PrescriptionDrafted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPrescription(prescription);
    }

    public async Task<PrescriptionDetails> UpdatePrescriptionDraftAsync(
        Guid userId,
        Guid prescriptionId,
        string? diagnosis,
        string? symptoms,
        string? clinicalObservations,
        string? advice,
        DateOnly? followUpDate,
        IReadOnlyList<MedicineInput> medicines,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var prescription = await store.FindPrescriptionForMutationAsync(prescriptionId, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        if (prescription.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        }

        var version = prescription.CurrentVersion;
        await store.DeleteItemsForVersionAsync(version.Id, cancellationToken);
        prescription.UpdateDraftFields(diagnosis, symptoms, clinicalObservations, advice, followUpDate, clock.UtcNow);
        var medicineOrder = 0;
        foreach (var item in medicines)
        {
            store.AddPrescriptionItem(PrescriptionItem.Create(
                version.Id,
                item.CatalogEntryId,
                item.MedicineName,
                item.Dosage,
                item.Frequency,
                item.Duration,
                item.Route,
                item.Instructions,
                medicineOrder++));
        }
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, prescription.MedicalRecordId, MedicalAccessAction.PrescriptionDrafted, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.SaveChangesAsync(cancellationToken);
        return MapPrescription(prescription);
    }

    public async Task<PrescriptionDetails> FinalizePrescriptionAsync(
        Guid userId,
        Guid prescriptionId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var prescription = await store.FindPrescriptionAsync(prescriptionId, true, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        if (prescription.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        }

        var version = prescription.Finalize(userId, clock.UtcNow, null);
        var pdfBytes = await pdfs.GenerateAsync(prescription, version, cancellationToken);
        await using var stream = new MemoryStream(pdfBytes);
        var stored = await documents.SaveAsync(stream, ".pdf", cancellationToken);
        version.AttachPdf(stored.StorageKey);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, prescription.MedicalRecordId, MedicalAccessAction.PrescriptionFinalized, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.AddAuditAsync(
            Audit(userId, version.AmendedFromVersionId is null ? AuditActions.PrescriptionFinalized : AuditActions.PrescriptionAmended, metadata),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        try
        {
            Appointment? appointment = null;
            if (prescription.AppointmentId is Guid appointmentId)
            {
                appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken);
            }

            await automation.PublishAsync(
                AutomationTriggerTypes.PrescriptionFinalized,
                new
                {
                    prescriptionId = prescription.Id,
                    appointmentId = prescription.AppointmentId,
                    userId = appointment?.AccountUserId,
                    doctorId = prescription.DoctorId
                },
                $"rx.finalized:{prescription.Id:N}:{version.Id:N}",
                cancellationToken,
                subjectId: prescription.Id,
                userId: appointment?.AccountUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish prescription automation event {PrescriptionId}", prescription.Id);
        }

        return MapPrescription(prescription);
    }

    public async Task<PrescriptionDetails> AmendPrescriptionAsync(
        Guid userId,
        Guid prescriptionId,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var prescription = await store.FindPrescriptionForMutationAsync(prescriptionId, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        if (prescription.DoctorId != doctor.Id)
        {
            throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        }

        var previousItems = await store.ListVersionItemsAsync(prescription.CurrentVersion.Id, cancellationToken);
        var draft = prescription.BeginAmendment(userId, reason, clock.UtcNow);
        store.AddPrescriptionVersion(draft);
        var order = 0;
        foreach (var item in previousItems)
        {
            store.AddPrescriptionItem(PrescriptionItem.Create(
                draft.Id,
                item.MedicineCatalogEntryId,
                item.MedicineName,
                item.Dosage,
                item.Frequency,
                item.Duration,
                item.Route,
                item.Instructions,
                order++));
        }

        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, prescription.MedicalRecordId, MedicalAccessAction.PrescriptionAmended, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.AddAuditAsync(Audit(userId, AuditActions.PrescriptionAmended, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        var reloaded = await store.FindPrescriptionAsync(prescriptionId, false, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        return MapPrescription(reloaded);
    }

    public async Task<PrescriptionDetails> GetPrescriptionAsync(
        Guid userId,
        Guid prescriptionId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var prescription = await store.FindPrescriptionAsync(prescriptionId, false, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        await EnsureCanReadRecordAsync(userId, prescription.MedicalRecordId, prescription.AccountUserId, prescription.FamilyMemberId, prescription.DoctorId, cancellationToken);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, prescription.MedicalRecordId, MedicalAccessAction.PrescriptionViewed, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.SaveChangesAsync(cancellationToken);
        return MapPrescription(prescription);
    }

    public async Task<StoredPdf> DownloadPrescriptionPdfAsync(
        Guid userId,
        Guid prescriptionId,
        int? versionNumber,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var prescription = await store.FindPrescriptionAsync(prescriptionId, false, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        await EnsureCanReadRecordAsync(userId, prescription.MedicalRecordId, prescription.AccountUserId, prescription.FamilyMemberId, prescription.DoctorId, cancellationToken);
        var version = versionNumber is null
            ? prescription.CurrentVersion
            : prescription.Versions.SingleOrDefault(item => item.VersionNumber == versionNumber)
              ?? throw DomainException.NotFound("prescription_version_not_found", "That prescription version was not found.");
        if (version.Status == PrescriptionStatus.Draft || string.IsNullOrWhiteSpace(version.PdfStorageKey))
        {
            throw DomainException.Conflict("prescription_pdf_unavailable", "A PDF is available only after the prescription is finalized.");
        }

        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, prescription.MedicalRecordId, MedicalAccessAction.PrescriptionPdfDownloaded, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, prescriptionId: prescription.Id));
        await store.SaveChangesAsync(cancellationToken);
        var stream = await documents.OpenReadAsync(version.PdfStorageKey, cancellationToken);
        return new StoredPdf(stream, $"{prescription.Number}-v{version.VersionNumber}.pdf", "application/pdf");
    }

    public async Task<IReadOnlyList<MedicalRecordSummary>> ListMyRecordsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var self = await store.FindRecordAsync(userId, null, false, cancellationToken);
        if (self is null)
        {
            var patientName = "Me";
            await EnsureRecordAsync(userId, null, patientName, cancellationToken);
        }

        var rows = await store.ListRecordsForAccountAsync(userId, cancellationToken);
        return rows.Select(row => new MedicalRecordSummary(
            row.Id,
            row.AccountUserId,
            row.FamilyMemberId,
            row.SubjectName,
            row.UpdatedAtUtc,
            row.Consultations.Count,
            row.Prescriptions.Count,
            row.Documents.Count)).ToArray();
    }

    public async Task<MedicalRecordDetails> GetRecordAsync(
        Guid userId,
        Guid recordId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var record = await store.FindRecordByIdAsync(recordId, false, cancellationToken)
            ?? throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
        await EnsureCanReadRecordAsync(userId, record.Id, record.AccountUserId, record.FamilyMemberId, null, cancellationToken);
        await AccessAsync(userId, record.Id, MedicalAccessAction.RecordViewed, metadata, cancellationToken);
        return await MapRecordAsync(record, cancellationToken);
    }

    public async Task<AllergyDetails> UpsertAllergyAsync(
        Guid userId,
        Guid recordId,
        Guid? allergyId,
        string substance,
        string? reaction,
        string severity,
        bool isActive,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var record = await RequireWritableRecordAsync(userId, doctor, recordId, cancellationToken);
        if (!Enum.TryParse<AllergySeverity>(severity, true, out var parsed))
        {
            throw DomainException.Validation("allergy_severity_invalid", "Choose a valid allergy severity.");
        }

        PatientAllergy allergy;
        if (allergyId is null)
        {
            allergy = PatientAllergy.Record(record.Id, substance, reaction, parsed, userId, doctor.Id, clock.UtcNow);
            store.AddAllergy(allergy);
        }
        else
        {
            allergy = await store.FindAllergyAsync(allergyId.Value, true, cancellationToken)
                ?? throw DomainException.NotFound("allergy_not_found", "That allergy was not found.");
            if (allergy.MedicalRecordId != record.Id)
            {
                throw DomainException.NotFound("allergy_not_found", "That allergy was not found.");
            }

            allergy.Update(substance, reaction, parsed, isActive, clock.UtcNow);
        }

        record.Touch(clock.UtcNow);
        await ChartChangedAsync(userId, record.Id, metadata, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapAllergy(allergy);
    }

    public async Task<ConditionDetails> UpsertConditionAsync(
        Guid userId,
        Guid recordId,
        Guid? conditionId,
        string name,
        string? notes,
        DateOnly? onsetDate,
        bool isActive,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var record = await RequireWritableRecordAsync(userId, doctor, recordId, cancellationToken);
        PatientMedicalCondition condition;
        if (conditionId is null)
        {
            condition = PatientMedicalCondition.Record(record.Id, name, notes, onsetDate, userId, doctor.Id, clock.UtcNow);
            store.AddCondition(condition);
        }
        else
        {
            condition = await store.FindConditionAsync(conditionId.Value, true, cancellationToken)
                ?? throw DomainException.NotFound("condition_not_found", "That condition was not found.");
            if (condition.MedicalRecordId != record.Id)
            {
                throw DomainException.NotFound("condition_not_found", "That condition was not found.");
            }

            condition.Update(name, notes, onsetDate, isActive, clock.UtcNow);
        }

        record.Touch(clock.UtcNow);
        await ChartChangedAsync(userId, record.Id, metadata, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapCondition(condition);
    }

    public async Task<MedicationDetails> UpsertMedicationAsync(
        Guid userId,
        Guid recordId,
        Guid? medicationId,
        string name,
        string? dosage,
        string? frequency,
        bool isCurrent,
        DateOnly? startedOn,
        DateOnly? endedOn,
        string? notes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
        var record = await RequireWritableRecordAsync(userId, doctor, recordId, cancellationToken);
        PatientMedication medication;
        if (medicationId is null)
        {
            medication = PatientMedication.Record(record.Id, name, dosage, frequency, isCurrent, startedOn, endedOn, notes, userId, doctor.Id, clock.UtcNow);
            store.AddMedication(medication);
        }
        else
        {
            medication = await store.FindMedicationAsync(medicationId.Value, true, cancellationToken)
                ?? throw DomainException.NotFound("medication_not_found", "That medication was not found.");
            if (medication.MedicalRecordId != record.Id)
            {
                throw DomainException.NotFound("medication_not_found", "That medication was not found.");
            }

            medication.Update(name, dosage, frequency, isCurrent, startedOn, endedOn, notes, clock.UtcNow);
        }

        record.Touch(clock.UtcNow);
        await ChartChangedAsync(userId, record.Id, metadata, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapMedication(medication);
    }

    public async Task<MedicalDocumentDetails> UploadDocumentAsync(
        Guid userId,
        Guid recordId,
        string title,
        string fileName,
        Stream content,
        string contentType,
        string? notes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var record = await store.FindRecordByIdAsync(recordId, true, cancellationToken)
            ?? throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
        var isPatient = record.AccountUserId == userId;
        Doctor? doctor = null;
        if (!isPatient)
        {
            doctor = await RequireApprovedDoctorAsync(userId, cancellationToken);
            if (!await store.DoctorHasCareRelationshipAsync(doctor.Id, record.AccountUserId, record.FamilyMemberId, cancellationToken)
                && !await DoctorOwnsRelatedConsultationAsync(doctor.Id, record.Id, cancellationToken))
            {
                throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
            }
        }

        var scan = await scanner.ScanAsync(content, fileName, contentType, imagesOnly: false, cancellationToken);
        if (!scan.Allowed || scan.ContentType is null)
        {
            throw DomainException.Validation("document_type_invalid", "Upload a PDF, JPG, PNG, or WebP file.");
        }

        content.Position = 0;
        var extension = scan.ContentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw DomainException.Validation("document_type_invalid", "Upload a PDF, JPG, PNG, or WebP file.")
        };
        var stored = await documents.SaveAsync(content, extension, cancellationToken);
        var document = MedicalDocument.Create(
            record.Id,
            userId,
            title,
            fileName,
            scan.ContentType,
            stored.StorageKey,
            stored.SizeBytes,
            notes,
            clock.UtcNow);
        store.AddDocument(document);
        record.Touch(clock.UtcNow);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, record.Id, MedicalAccessAction.DocumentUploaded, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, documentId: document.Id));
        await store.AddAuditAsync(Audit(userId, AuditActions.MedicalDocumentUploaded, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapDocument(document);
    }

    public async Task<StoredPdf> DownloadDocumentAsync(
        Guid userId,
        Guid documentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var document = await store.FindDocumentAsync(documentId, false, cancellationToken)
            ?? throw DomainException.NotFound("document_not_found", "That document was not found.");
        var record = await store.FindRecordByIdAsync(document.MedicalRecordId, false, cancellationToken)
            ?? throw DomainException.NotFound("document_not_found", "That document was not found.");
        await EnsureCanReadRecordAsync(userId, record.Id, record.AccountUserId, record.FamilyMemberId, null, cancellationToken);
        store.AddAccessLog(MedicalRecordAccessLog.Record(
            userId, record.Id, MedicalAccessAction.DocumentDownloaded, clock.UtcNow, metadata.IpAddress, metadata.UserAgent, documentId: document.Id));
        await store.SaveChangesAsync(cancellationToken);
        var stream = await documents.OpenReadAsync(document.StorageKey, cancellationToken);
        return new StoredPdf(stream, document.FileName, document.ContentType);
    }

    public async Task<IReadOnlyList<MedicineCatalogItem>> SearchMedicinesAsync(string? query, CancellationToken cancellationToken)
    {
        var rows = await store.SearchMedicinesAsync(query ?? string.Empty, 20, cancellationToken);
        return rows.Select(item => new MedicineCatalogItem(item.Id, item.Name, item.Form, item.StrengthHint)).ToArray();
    }

    public async Task<MedicineCatalogItem> AddMedicineCatalogAsync(Guid userId, string name, string? form, string? strengthHint, CancellationToken cancellationToken)
    {
        await RequireApprovedDoctorAsync(userId, cancellationToken);
        var entry = MedicineCatalogEntry.Create(name, form, strengthHint, clock.UtcNow);
        store.AddCatalogEntry(entry);
        await store.SaveChangesAsync(cancellationToken);
        return new MedicineCatalogItem(entry.Id, entry.Name, entry.Form, entry.StrengthHint);
    }

    private async Task<bool> DoctorOwnsRelatedConsultationAsync(Guid doctorId, Guid recordId, CancellationToken cancellationToken)
    {
        var consultations = await store.ListConsultationsAsync(recordId, cancellationToken);
        return consultations.Any(item => item.DoctorId == doctorId);
    }

    private async Task<PatientMedicalRecord> RequireWritableRecordAsync(Guid userId, Doctor doctor, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await store.FindRecordByIdAsync(recordId, true, cancellationToken)
            ?? throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
        if (!await store.DoctorHasCareRelationshipAsync(doctor.Id, record.AccountUserId, record.FamilyMemberId, cancellationToken)
            && !await DoctorOwnsRelatedConsultationAsync(doctor.Id, record.Id, cancellationToken))
        {
            throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
        }

        return record;
    }

    private async Task EnsureCanReadRecordAsync(
        Guid userId,
        Guid recordId,
        Guid accountUserId,
        Guid? familyMemberId,
        Guid? treatingDoctorId,
        CancellationToken cancellationToken)
    {
        if (accountUserId == userId)
        {
            return;
        }

        var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken);
        if (doctor is null || doctor.Status != VerificationStatus.Approved)
        {
            throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
        }

        if (treatingDoctorId == doctor.Id)
        {
            return;
        }

        if (await store.DoctorHasCareRelationshipAsync(doctor.Id, accountUserId, familyMemberId, cancellationToken))
        {
            return;
        }

        if (await DoctorOwnsRelatedConsultationAsync(doctor.Id, recordId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("medical_record_not_found", "That medical record was not found.");
    }

    private async Task<Doctor> RequireApprovedDoctorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "A doctor profile is required.");
        if (doctor.Status != VerificationStatus.Approved)
        {
            throw DomainException.Conflict("doctor_not_verified", "Only an approved doctor can manage clinical records.");
        }

        return doctor;
    }

    private async Task<PatientMedicalRecord> EnsureRecordAsync(
        Guid accountUserId,
        Guid? familyMemberId,
        string subjectName,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindRecordAsync(accountUserId, familyMemberId, true, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SubjectName, subjectName, StringComparison.Ordinal))
            {
                existing.Rename(subjectName, clock.UtcNow);
            }

            return existing;
        }

        var record = PatientMedicalRecord.Open(accountUserId, familyMemberId, subjectName, clock.UtcNow);
        store.AddRecord(record);
        await store.SaveChangesAsync(cancellationToken);
        return record;
    }

    private async Task AccessAsync(Guid userId, Guid recordId, MedicalAccessAction action, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        store.AddAccessLog(MedicalRecordAccessLog.Record(userId, recordId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent));
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task ChartChangedAsync(Guid userId, Guid recordId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        store.AddAccessLog(MedicalRecordAccessLog.Record(userId, recordId, MedicalAccessAction.AllergyChanged, clock.UtcNow, metadata.IpAddress, metadata.UserAgent));
        await store.AddAuditAsync(Audit(userId, AuditActions.MedicalChartChanged, metadata), cancellationToken);
    }

    private async Task<MedicalRecordDetails> MapRecordAsync(PatientMedicalRecord record, CancellationToken cancellationToken)
    {
        var loaded = await store.FindRecordByIdAsync(record.Id, false, cancellationToken) ?? record;
        var consultations = await store.ListConsultationsAsync(loaded.Id, cancellationToken);
        var prescriptions = await store.ListPrescriptionsAsync(loaded.Id, cancellationToken);
        var doctorIds = consultations.Select(item => item.DoctorId).Concat(prescriptions.Select(item => item.DoctorId)).Distinct();
        var clinicIds = consultations.Select(item => item.ClinicId).Concat(prescriptions.Select(item => item.ClinicId)).Distinct();
        var branchIds = consultations.Select(item => item.BranchId).Concat(prescriptions.Select(item => item.BranchId)).Distinct();
        var doctors = await store.DoctorNamesAsync(doctorIds, cancellationToken);
        var clinics = await store.ClinicNamesAsync(clinicIds, cancellationToken);
        var branches = await store.BranchNamesAsync(branchIds, cancellationToken);
        return new MedicalRecordDetails(
            loaded.Id,
            loaded.AccountUserId,
            loaded.FamilyMemberId,
            loaded.SubjectName,
            loaded.CreatedAtUtc,
            loaded.UpdatedAtUtc,
            loaded.Allergies.OrderByDescending(item => item.RecordedAtUtc).Select(MapAllergy).ToArray(),
            loaded.Conditions.OrderByDescending(item => item.RecordedAtUtc).Select(MapCondition).ToArray(),
            loaded.Medications.OrderByDescending(item => item.RecordedAtUtc).Select(MapMedication).ToArray(),
            consultations.OrderByDescending(item => item.StartedAtUtc).Select(item => new ConsultationSummary(
                item.Id,
                item.MedicalRecordId,
                item.DoctorId,
                doctors.GetValueOrDefault(item.DoctorId, "Doctor"),
                item.ClinicId,
                clinics.GetValueOrDefault(item.ClinicId, "Clinic"),
                item.BranchId,
                branches.GetValueOrDefault(item.BranchId, "Branch"),
                item.AppointmentId,
                item.PatientName,
                item.Status.ToString(),
                item.StartedAtUtc,
                item.CompletedAtUtc,
                item.Diagnoses.OrderBy(d => d.SortOrder).Select(d => d.Description).ToArray())).ToArray(),
            prescriptions.OrderByDescending(item => item.UpdatedAtUtc).Select(MapPrescriptionSummary).ToArray(),
            loaded.Documents.OrderByDescending(item => item.UploadedAtUtc).Select(MapDocument).ToArray());
    }

    private async Task<ConsultationDetails> MapConsultationAsync(ConsultationRecord consultation, CancellationToken cancellationToken)
    {
        var doctors = await store.DoctorNamesAsync([consultation.DoctorId], cancellationToken);
        var clinics = await store.ClinicNamesAsync([consultation.ClinicId], cancellationToken);
        var branches = await store.BranchNamesAsync([consultation.BranchId], cancellationToken);
        var prescriptions = await store.ListPrescriptionsAsync(consultation.MedicalRecordId, cancellationToken);
        return new ConsultationDetails(
            consultation.Id,
            consultation.MedicalRecordId,
            consultation.DoctorId,
            doctors.GetValueOrDefault(consultation.DoctorId, "Doctor"),
            consultation.ClinicId,
            clinics.GetValueOrDefault(consultation.ClinicId, "Clinic"),
            consultation.BranchId,
            branches.GetValueOrDefault(consultation.BranchId, "Branch"),
            consultation.AppointmentId,
            consultation.AccountUserId,
            consultation.FamilyMemberId,
            consultation.PatientName,
            consultation.Symptoms,
            consultation.ClinicalObservations,
            consultation.DoctorNotes,
            consultation.Advice,
            consultation.FollowUpDate,
            consultation.Status.ToString(),
            consultation.StartedAtUtc,
            consultation.UpdatedAtUtc,
            consultation.CompletedAtUtc,
            consultation.Diagnoses.OrderBy(item => item.SortOrder).Select(item => new DiagnosisDetails(item.Id, item.Description, item.Code, item.IsPrimary, item.SortOrder)).ToArray(),
            prescriptions.Where(item => item.ConsultationRecordId == consultation.Id).Select(MapPrescriptionSummary).ToArray());
    }

    private static PrescriptionDetails MapPrescription(Prescription prescription) =>
        new(
            prescription.Id,
            prescription.Number,
            prescription.MedicalRecordId,
            prescription.ConsultationRecordId,
            prescription.DoctorId,
            prescription.ClinicId,
            prescription.BranchId,
            prescription.AccountUserId,
            prescription.FamilyMemberId,
            prescription.Status.ToString(),
            prescription.CurrentVersionNumber,
            prescription.CreatedAtUtc,
            prescription.UpdatedAtUtc,
            MapVersion(prescription.CurrentVersion),
            prescription.Versions.OrderByDescending(item => item.VersionNumber).Select(MapVersion).ToArray());

    private static PrescriptionSummary MapPrescriptionSummary(Prescription prescription) =>
        new(
            prescription.Id,
            prescription.Number,
            prescription.Status.ToString(),
            prescription.CurrentVersionNumber,
            prescription.UpdatedAtUtc,
            prescription.CurrentVersion.Diagnosis,
            prescription.CurrentVersion.FinalizedAtUtc);

    private static PrescriptionVersionDetails MapVersion(PrescriptionVersion version) =>
        new(
            version.Id,
            version.VersionNumber,
            version.Status.ToString(),
            version.Diagnosis,
            version.Symptoms,
            version.ClinicalObservations,
            version.Advice,
            version.FollowUpDate,
            version.PatientNameSnapshot,
            version.DoctorNameSnapshot,
            version.DoctorRegistrationSnapshot,
            version.ClinicNameSnapshot,
            version.BranchNameSnapshot,
            version.BranchAddressSnapshot,
            version.ConsultationDateUtc,
            version.AmendedFromVersionId,
            version.AmendmentReason,
            version.CreatedAtUtc,
            version.FinalizedAtUtc,
            !string.IsNullOrWhiteSpace(version.PdfStorageKey),
            version.Items.OrderBy(item => item.SortOrder).Select(item => new MedicineDetails(
                item.Id,
                item.MedicineCatalogEntryId,
                item.MedicineName,
                item.Dosage,
                item.Frequency,
                item.Duration,
                item.Route,
                item.Instructions,
                item.SortOrder)).ToArray());

    private static AllergyDetails MapAllergy(PatientAllergy allergy) =>
        new(allergy.Id, allergy.Substance, allergy.Reaction, allergy.Severity.ToString(), allergy.IsActive, allergy.RecordedAtUtc);

    private static ConditionDetails MapCondition(PatientMedicalCondition condition) =>
        new(condition.Id, condition.Name, condition.Notes, condition.OnsetDate, condition.IsActive, condition.RecordedAtUtc);

    private static MedicationDetails MapMedication(PatientMedication medication) =>
        new(medication.Id, medication.Name, medication.Dosage, medication.Frequency, medication.IsCurrent, medication.StartedOn, medication.EndedOn, medication.Notes, medication.RecordedAtUtc);

    private static MedicalDocumentDetails MapDocument(MedicalDocument document) =>
        new(document.Id, document.Title, document.FileName, document.ContentType, document.SizeBytes, document.Notes, document.UploadedAtUtc);

    private static string? FormatAddress(ClinicBranch branch)
    {
        var parts = new[] { branch.AddressLine, branch.AddressLine2, branch.City, branch.State, branch.PostalCode }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var text = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
