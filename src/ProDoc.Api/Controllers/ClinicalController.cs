using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Clinical;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class ClinicalController(MedicalService medical) : ControllerBase
{
    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpGet("appointments/{appointmentId:guid}/workspace")]
    public Task<WorkspaceDetails> Workspace(Guid appointmentId, CancellationToken cancellationToken) =>
        medical.OpenWorkspaceAsync(User.GetUserId(), appointmentId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("appointments/{appointmentId:guid}/consultations")]
    public Task<ConsultationDetails> StartConsultation(Guid appointmentId, CancellationToken cancellationToken) =>
        medical.StartConsultationAsync(User.GetUserId(), appointmentId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPut("consultations/{consultationId:guid}")]
    public Task<ConsultationDetails> UpdateConsultation(Guid consultationId, UpdateConsultationRequest request, CancellationToken cancellationToken) =>
        medical.UpdateConsultationAsync(
            User.GetUserId(),
            consultationId,
            request.Symptoms,
            request.ClinicalObservations,
            request.DoctorNotes,
            request.Advice,
            request.FollowUpDate,
            request.Diagnoses ?? [],
            request.Complete,
            Meta(),
            cancellationToken);

    [HttpGet("consultations/{consultationId:guid}")]
    public Task<ConsultationDetails> GetConsultation(Guid consultationId, CancellationToken cancellationToken) =>
        medical.GetConsultationAsync(User.GetUserId(), consultationId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpPost("consultations/{consultationId:guid}/prescriptions")]
    public Task<PrescriptionDetails> CreatePrescription(Guid consultationId, CancellationToken cancellationToken) =>
        medical.CreatePrescriptionDraftAsync(User.GetUserId(), consultationId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpPut("prescriptions/{prescriptionId:guid}")]
    public Task<PrescriptionDetails> UpdatePrescription(Guid prescriptionId, UpdatePrescriptionRequest request, CancellationToken cancellationToken) =>
        medical.UpdatePrescriptionDraftAsync(
            User.GetUserId(),
            prescriptionId,
            request.Diagnosis,
            request.Symptoms,
            request.ClinicalObservations,
            request.Advice,
            request.FollowUpDate,
            request.Medicines ?? [],
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpPost("prescriptions/{prescriptionId:guid}/finalize")]
    public Task<PrescriptionDetails> Finalize(Guid prescriptionId, CancellationToken cancellationToken) =>
        medical.FinalizePrescriptionAsync(User.GetUserId(), prescriptionId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpPost("prescriptions/{prescriptionId:guid}/amend")]
    public Task<PrescriptionDetails> Amend(Guid prescriptionId, AmendPrescriptionRequest request, CancellationToken cancellationToken) =>
        medical.AmendPrescriptionAsync(User.GetUserId(), prescriptionId, request.Reason, Meta(), cancellationToken);

    [HttpGet("prescriptions/{prescriptionId:guid}")]
    public Task<PrescriptionDetails> GetPrescription(Guid prescriptionId, CancellationToken cancellationToken) =>
        medical.GetPrescriptionAsync(User.GetUserId(), prescriptionId, Meta(), cancellationToken);

    [HttpGet("prescriptions/{prescriptionId:guid}/pdf")]
    public async Task<IActionResult> PrescriptionPdf(Guid prescriptionId, [FromQuery] int? version, CancellationToken cancellationToken)
    {
        var pdf = await medical.DownloadPrescriptionPdfAsync(User.GetUserId(), prescriptionId, version, Meta(), cancellationToken);
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    [HasPermission(PermissionCodes.RecordsReadSelf)]
    [HttpGet("patients/me/medical-records")]
    public Task<IReadOnlyList<MedicalRecordSummary>> MyRecords(CancellationToken cancellationToken) =>
        medical.ListMyRecordsAsync(User.GetUserId(), cancellationToken);

    [HttpGet("medical-records/{recordId:guid}")]
    public Task<MedicalRecordDetails> GetRecord(Guid recordId, CancellationToken cancellationToken) =>
        medical.GetRecordAsync(User.GetUserId(), recordId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("medical-records/{recordId:guid}/allergies")]
    public Task<AllergyDetails> UpsertAllergy(Guid recordId, UpsertAllergyRequest request, CancellationToken cancellationToken) =>
        medical.UpsertAllergyAsync(
            User.GetUserId(),
            recordId,
            request.AllergyId,
            request.Substance,
            request.Reaction,
            request.Severity,
            request.IsActive,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("medical-records/{recordId:guid}/conditions")]
    public Task<ConditionDetails> UpsertCondition(Guid recordId, UpsertConditionRequest request, CancellationToken cancellationToken) =>
        medical.UpsertConditionAsync(
            User.GetUserId(),
            recordId,
            request.ConditionId,
            request.Name,
            request.Notes,
            request.OnsetDate,
            request.IsActive,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("medical-records/{recordId:guid}/medications")]
    public Task<MedicationDetails> UpsertMedication(Guid recordId, UpsertMedicationRequest request, CancellationToken cancellationToken) =>
        medical.UpsertMedicationAsync(
            User.GetUserId(),
            recordId,
            request.MedicationId,
            request.Name,
            request.Dosage,
            request.Frequency,
            request.IsCurrent,
            request.StartedOn,
            request.EndedOn,
            request.Notes,
            Meta(),
            cancellationToken);

    [HttpPost("medical-records/{recordId:guid}/documents")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<MedicalDocumentDetails> UploadDocument(Guid recordId, IFormFile file, [FromForm] string title, [FromForm] string? notes, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await medical.UploadDocumentAsync(
            User.GetUserId(),
            recordId,
            title,
            file.FileName,
            stream,
            file.ContentType,
            notes,
            Meta(),
            cancellationToken);
    }

    [HttpGet("medical-documents/{documentId:guid}/content")]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken)
    {
        var file = await medical.DownloadDocumentAsync(User.GetUserId(), documentId, Meta(), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpGet("medicines")]
    public Task<IReadOnlyList<MedicineCatalogItem>> SearchMedicines([FromQuery] string? q, CancellationToken cancellationToken) =>
        medical.SearchMedicinesAsync(q, cancellationToken);

    [HasPermission(PermissionCodes.PrescriptionsPublish)]
    [HttpPost("medicines")]
    public Task<MedicineCatalogItem> AddMedicine(AddMedicineCatalogRequest request, CancellationToken cancellationToken) =>
        medical.AddMedicineCatalogAsync(User.GetUserId(), request.Name, request.Form, request.StrengthHint, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
