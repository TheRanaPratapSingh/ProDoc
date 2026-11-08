using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Clinical;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Clinical;

public sealed class MedicalStore(ProDocDbContext db) : IMedicalStore
{
    public Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Doctors.Include(doctor => doctor.Registrations).SingleOrDefaultAsync(doctor => doctor.UserId == userId, cancellationToken);

    public Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Doctors.AsQueryable() : db.Doctors.AsNoTracking();
        return query.Include(doctor => doctor.Registrations).SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);
    }

    public Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().Include(clinic => clinic.Branches).SingleOrDefaultAsync(clinic => clinic.Id == clinicId, cancellationToken);

    public Task<ClinicBranch?> FindBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        db.ClinicBranches.AsNoTracking().SingleOrDefaultAsync(branch => branch.Id == branchId, cancellationToken);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments.AsQueryable() : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
    }

    public async Task<bool> DoctorHasCareRelationshipAsync(
        Guid doctorId,
        Guid accountUserId,
        Guid? familyMemberId,
        CancellationToken cancellationToken)
    {
        // SQL NULL = NULL is unknown — compare null family members explicitly with IS NULL.
        return await db.Appointments.AsNoTracking().AnyAsync(
            item => item.DoctorId == doctorId
                && item.AccountUserId == accountUserId
                && (familyMemberId == null
                    ? item.FamilyMemberId == null
                    : item.FamilyMemberId == familyMemberId)
                && (item.Status == AppointmentStatus.Confirmed
                    || item.Status == AppointmentStatus.CheckedIn
                    || item.Status == AppointmentStatus.Completed
                    || item.Status == AppointmentStatus.Requested),
            cancellationToken);
    }

    public Task<PatientMedicalRecord?> FindRecordAsync(
        Guid accountUserId,
        Guid? familyMemberId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = RecordQuery(tracking);
        return familyMemberId is null
            ? query.SingleOrDefaultAsync(item => item.AccountUserId == accountUserId && item.FamilyMemberId == null, cancellationToken)
            : query.SingleOrDefaultAsync(item => item.AccountUserId == accountUserId && item.FamilyMemberId == familyMemberId, cancellationToken);
    }

    public Task<PatientMedicalRecord?> FindRecordByIdAsync(Guid recordId, bool tracking, CancellationToken cancellationToken) =>
        RecordQuery(tracking).SingleOrDefaultAsync(item => item.Id == recordId, cancellationToken);

    public void AddRecord(PatientMedicalRecord record) => db.PatientMedicalRecords.Add(record);

    public Task<ConsultationRecord?> FindConsultationAsync(Guid consultationId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<ConsultationRecord> query = tracking ? db.ConsultationRecords : db.ConsultationRecords.AsNoTracking();
        if (!tracking)
        {
            query = query.Include(item => item.Diagnoses);
        }

        return query.SingleOrDefaultAsync(item => item.Id == consultationId, cancellationToken);
    }

    public Task<ConsultationRecord?> FindActiveConsultationForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.ConsultationRecords.AsNoTracking()
            .Include(item => item.Diagnoses)
            .SingleOrDefaultAsync(
                item => item.AppointmentId == appointmentId && item.Status == ConsultationStatus.InProgress,
                cancellationToken);

    public async Task<IReadOnlyList<ConsultationRecord>> ListConsultationsAsync(Guid medicalRecordId, CancellationToken cancellationToken) =>
        await db.ConsultationRecords.AsNoTracking()
            .Include(item => item.Diagnoses)
            .Where(item => item.MedicalRecordId == medicalRecordId)
            .OrderByDescending(item => item.StartedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddConsultation(ConsultationRecord consultation) => db.ConsultationRecords.Add(consultation);

    public Task DeleteDiagnosesForConsultationAsync(Guid consultationId, CancellationToken cancellationToken) =>
        db.ConsultationDiagnoses.Where(item => item.ConsultationRecordId == consultationId).ExecuteDeleteAsync(cancellationToken);

    public void AddDiagnosis(ConsultationDiagnosis diagnosis) => db.ConsultationDiagnoses.Add(diagnosis);

    public Task DeleteItemsForVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        db.PrescriptionItems.Where(item => item.PrescriptionVersionId == versionId).ExecuteDeleteAsync(cancellationToken);

    public void AddPrescriptionItem(PrescriptionItem item) => db.PrescriptionItems.Add(item);

    public Task<Prescription?> FindPrescriptionAsync(Guid prescriptionId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Prescriptions.AsQueryable() : db.Prescriptions.AsNoTracking();
        return query.Include(item => item.Versions).ThenInclude(version => version.Items)
            .SingleOrDefaultAsync(item => item.Id == prescriptionId, cancellationToken);
    }

    public Task<Prescription?> FindPrescriptionForMutationAsync(Guid prescriptionId, CancellationToken cancellationToken) =>
        db.Prescriptions.Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == prescriptionId, cancellationToken);

    public async Task<IReadOnlyList<PrescriptionItem>> ListVersionItemsAsync(Guid versionId, CancellationToken cancellationToken) =>
        await db.PrescriptionItems.AsNoTracking()
            .Where(item => item.PrescriptionVersionId == versionId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Prescription>> ListPrescriptionsAsync(Guid medicalRecordId, CancellationToken cancellationToken) =>
        await db.Prescriptions.AsNoTracking()
            .Include(item => item.Versions).ThenInclude(version => version.Items)
            .Where(item => item.MedicalRecordId == medicalRecordId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddPrescription(Prescription prescription) => db.Prescriptions.Add(prescription);

    public void AddPrescriptionVersion(PrescriptionVersion version) => db.PrescriptionVersions.Add(version);

    public async Task<string> NextPrescriptionNumberAsync(CancellationToken cancellationToken)
    {
        var day = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"RX{day}";
        var latest = await db.Prescriptions.AsNoTracking()
            .Where(item => item.Number.StartsWith(prefix))
            .OrderByDescending(item => item.Number)
            .Select(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = 1;
        if (latest is not null && latest.Length > prefix.Length && int.TryParse(latest[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{prefix}{next:D4}";
    }

    public Task<PatientAllergy?> FindAllergyAsync(Guid allergyId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PatientAllergies.AsQueryable() : db.PatientAllergies.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == allergyId, cancellationToken);
    }

    public void AddAllergy(PatientAllergy allergy) => db.PatientAllergies.Add(allergy);

    public Task<PatientMedicalCondition?> FindConditionAsync(Guid conditionId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PatientMedicalConditions.AsQueryable() : db.PatientMedicalConditions.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == conditionId, cancellationToken);
    }

    public void AddCondition(PatientMedicalCondition condition) => db.PatientMedicalConditions.Add(condition);

    public Task<PatientMedication?> FindMedicationAsync(Guid medicationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PatientMedications.AsQueryable() : db.PatientMedications.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == medicationId, cancellationToken);
    }

    public void AddMedication(PatientMedication medication) => db.PatientMedications.Add(medication);

    public Task<MedicalDocument?> FindDocumentAsync(Guid documentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.MedicalDocuments.AsQueryable() : db.MedicalDocuments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);
    }

    public void AddDocument(MedicalDocument document) => db.MedicalDocuments.Add(document);

    public async Task<IReadOnlyList<MedicineCatalogEntry>> SearchMedicinesAsync(string query, int take, CancellationToken cancellationToken)
    {
        var q = query.Trim();
        var rows = db.MedicineCatalog.AsNoTracking().Where(item => item.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            rows = rows.Where(item => item.Name.Contains(q));
        }

        return await rows.OrderBy(item => item.Name).Take(take).ToListAsync(cancellationToken);
    }

    public Task<MedicineCatalogEntry?> FindCatalogEntryAsync(Guid id, CancellationToken cancellationToken) =>
        db.MedicineCatalog.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void AddCatalogEntry(MedicineCatalogEntry entry) => db.MedicineCatalog.Add(entry);

    public void AddAccessLog(MedicalRecordAccessLog log) => db.MedicalRecordAccessLogs.Add(log);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> DoctorNamesAsync(IEnumerable<Guid> doctorIds, CancellationToken cancellationToken)
    {
        var ids = doctorIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.Doctors.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.DisplayName, item.FirstName, item.LastName })
            .ToListAsync(cancellationToken);
        var map = new Dictionary<Guid, string>();
        foreach (var item in rows)
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                map[item.Id] = item.DisplayName!;
                continue;
            }

            var composed = $"Dr. {item.FirstName} {item.LastName}".Trim();
            map[item.Id] = string.IsNullOrWhiteSpace(composed) ? "Doctor" : composed;
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ClinicNamesAsync(IEnumerable<Guid> clinicIds, CancellationToken cancellationToken)
    {
        var ids = clinicIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.Clinics.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.Id, item => item.Name ?? "Clinic");
    }

    public async Task<IReadOnlyDictionary<Guid, string>> BranchNamesAsync(IEnumerable<Guid> branchIds, CancellationToken cancellationToken)
    {
        var ids = branchIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.ClinicBranches.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.Id, item => item.Name ?? "Branch");
    }

    public Task<FamilyMember?> FindFamilyMemberAsync(Guid familyMemberId, CancellationToken cancellationToken) =>
        db.FamilyMembers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == familyMemberId, cancellationToken);

    public async Task<IReadOnlyList<PatientMedicalRecord>> ListRecordsForAccountAsync(Guid accountUserId, CancellationToken cancellationToken) =>
        await RecordQuery(false).Where(item => item.AccountUserId == accountUserId).OrderBy(item => item.SubjectName).ToListAsync(cancellationToken);

    public async Task<(int InProgress, int CompletedToday, int WithPrescriptionToday)> CountDoctorActivityAsync(
        Guid doctorId,
        DateOnly today,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var consultQuery = db.ConsultationRecords.AsNoTracking().Where(item => item.DoctorId == doctorId);
        if (clinicId is Guid clinic)
        {
            var appointmentIds = db.Appointments.AsNoTracking()
                .Where(item => item.DoctorId == doctorId && item.ClinicId == clinic)
                .Select(item => item.Id);
            consultQuery = consultQuery.Where(item => item.AppointmentId != null && appointmentIds.Contains(item.AppointmentId.Value));
        }

        var inProgress = await consultQuery.CountAsync(item => item.Status == ConsultationStatus.InProgress, cancellationToken);
        var completedToday = await consultQuery.CountAsync(
            item => item.Status == ConsultationStatus.Completed && item.UpdatedAtUtc >= dayStart && item.UpdatedAtUtc < dayEnd,
            cancellationToken);

        var rxQuery = db.Prescriptions.AsNoTracking().Where(item => item.DoctorId == doctorId && item.CreatedAtUtc >= dayStart && item.CreatedAtUtc < dayEnd);
        if (clinicId is Guid rxClinic)
        {
            var appointmentIds = db.Appointments.AsNoTracking()
                .Where(item => item.DoctorId == doctorId && item.ClinicId == rxClinic)
                .Select(item => item.Id);
            rxQuery = rxQuery.Where(item => item.AppointmentId != null && appointmentIds.Contains(item.AppointmentId.Value));
        }

        var withPrescription = await rxQuery.CountAsync(cancellationToken);
        return (inProgress, completedToday, withPrescription);
    }

    public async Task<IReadOnlyList<ProDoc.Application.Providers.DoctorPrescriptionRow>> ListDoctorPrescriptionsAsync(
        Guid doctorId,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var query =
            from prescription in db.Prescriptions.AsNoTracking().Include(item => item.Versions)
            join appointment in db.Appointments.AsNoTracking() on prescription.AppointmentId equals appointment.Id into appointmentJoin
            from appointment in appointmentJoin.DefaultIfEmpty()
            join clinic in db.Clinics.AsNoTracking() on appointment.ClinicId equals clinic.Id into clinicJoin
            from clinic in clinicJoin.DefaultIfEmpty()
            where prescription.DoctorId == doctorId
            select new { prescription, appointment, clinic };

        if (clinicId is Guid scopedClinic)
        {
            query = query.Where(item => item.appointment != null && item.appointment.ClinicId == scopedClinic);
        }

        var rows = await query.OrderByDescending(item => item.prescription.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);
        return rows.Select(item =>
        {
            var version = item.prescription.Versions.FirstOrDefault(version => version.VersionNumber == item.prescription.CurrentVersionNumber);
            return new ProDoc.Application.Providers.DoctorPrescriptionRow(
                item.appointment?.Id ?? Guid.Empty,
                item.appointment?.Reference,
                item.appointment?.PatientName ?? "Patient",
                item.prescription.Id,
                item.prescription.Number,
                item.prescription.Status.ToString(),
                version?.Diagnosis,
                item.appointment?.StartsAtUtc ?? item.prescription.CreatedAtUtc,
                item.appointment?.ClinicId ?? Guid.Empty,
                item.clinic?.Name ?? "Clinic");
        }).ToArray();
    }

    private IQueryable<PatientMedicalRecord> RecordQuery(bool tracking)
    {
        var query = tracking ? db.PatientMedicalRecords.AsQueryable() : db.PatientMedicalRecords.AsNoTracking();
        return query
            .Include(item => item.Allergies)
            .Include(item => item.Conditions)
            .Include(item => item.Medications)
            .Include(item => item.Documents)
            .Include(item => item.Consultations)
            .Include(item => item.Prescriptions);
    }
}
