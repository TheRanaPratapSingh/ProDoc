using ProDoc.Domain.Common;

namespace ProDoc.Domain.Clinical;

public sealed class PrescriptionItem
{
    private PrescriptionItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid PrescriptionVersionId { get; private set; }

    public Guid? MedicineCatalogEntryId { get; private set; }

    public string MedicineName { get; private set; } = string.Empty;

    public string Dosage { get; private set; } = string.Empty;

    public string Frequency { get; private set; } = string.Empty;

    public string Duration { get; private set; } = string.Empty;

    public string Route { get; private set; } = string.Empty;

    public string? Instructions { get; private set; }

    public int SortOrder { get; private set; }

    public static PrescriptionItem Create(
        Guid prescriptionVersionId,
        Guid? medicineCatalogEntryId,
        string medicineName,
        string dosage,
        string frequency,
        string duration,
        string route,
        string? instructions,
        int sortOrder)
    {
        return new PrescriptionItem
        {
            Id = Guid.NewGuid(),
            PrescriptionVersionId = prescriptionVersionId,
            MedicineCatalogEntryId = medicineCatalogEntryId,
            MedicineName = Required(medicineName, 2, 160, "medicine_name_invalid", "Enter a medicine name."),
            Dosage = Required(dosage, 1, 80, "dosage_invalid", "Enter the dosage."),
            Frequency = Required(frequency, 1, 80, "frequency_invalid", "Enter the frequency."),
            Duration = Required(duration, 1, 80, "duration_invalid", "Enter the duration."),
            Route = Required(route, 1, 80, "route_invalid", "Enter the route of administration."),
            Instructions = Optional(instructions, 500),
            SortOrder = sortOrder
        };
    }

    private static string Required(string value, int min, int max, string code, string title)
    {
        var text = value.Trim();
        if (text.Length < min || text.Length > max)
        {
            throw DomainException.Validation(code, title);
        }

        return text;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class PrescriptionVersion
{
    private readonly List<PrescriptionItem> _items = [];

    private PrescriptionVersion()
    {
    }

    public Guid Id { get; private set; }

    public Guid PrescriptionId { get; private set; }

    public int VersionNumber { get; private set; }

    public PrescriptionStatus Status { get; private set; }

    public string? Diagnosis { get; private set; }

    public string? Symptoms { get; private set; }

    public string? ClinicalObservations { get; private set; }

    public string? Advice { get; private set; }

    public DateOnly? FollowUpDate { get; private set; }

    public string PatientNameSnapshot { get; private set; } = string.Empty;

    public string DoctorNameSnapshot { get; private set; } = string.Empty;

    public string? DoctorRegistrationSnapshot { get; private set; }

    public string ClinicNameSnapshot { get; private set; } = string.Empty;

    public string BranchNameSnapshot { get; private set; } = string.Empty;

    public string? BranchAddressSnapshot { get; private set; }

    public DateTimeOffset ConsultationDateUtc { get; private set; }

    public Guid? AmendedFromVersionId { get; private set; }

    public string? AmendmentReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? FinalizedByUserId { get; private set; }

    public DateTimeOffset? FinalizedAtUtc { get; private set; }

    public string? PdfStorageKey { get; private set; }

    public IReadOnlyCollection<PrescriptionItem> Items => _items;

    public static PrescriptionVersion CreateDraft(
        Guid prescriptionId,
        int versionNumber,
        Guid createdByUserId,
        string patientName,
        string doctorName,
        string? doctorRegistration,
        string clinicName,
        string branchName,
        string? branchAddress,
        DateTimeOffset consultationDateUtc,
        DateTimeOffset now,
        Guid? amendedFromVersionId = null,
        string? amendmentReason = null)
    {
        return new PrescriptionVersion
        {
            Id = Guid.NewGuid(),
            PrescriptionId = prescriptionId,
            VersionNumber = versionNumber,
            Status = PrescriptionStatus.Draft,
            PatientNameSnapshot = patientName.Trim(),
            DoctorNameSnapshot = doctorName.Trim(),
            DoctorRegistrationSnapshot = string.IsNullOrWhiteSpace(doctorRegistration) ? null : doctorRegistration.Trim(),
            ClinicNameSnapshot = clinicName.Trim(),
            BranchNameSnapshot = branchName.Trim(),
            BranchAddressSnapshot = string.IsNullOrWhiteSpace(branchAddress) ? null : branchAddress.Trim(),
            ConsultationDateUtc = consultationDateUtc,
            AmendedFromVersionId = amendedFromVersionId,
            AmendmentReason = string.IsNullOrWhiteSpace(amendmentReason) ? null : amendmentReason.Trim()[..Math.Min(amendmentReason.Trim().Length, 500)],
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now
        };
    }

    public void ReplaceClinicalContent(
        string? diagnosis,
        string? symptoms,
        string? clinicalObservations,
        string? advice,
        DateOnly? followUpDate,
        IEnumerable<(Guid? CatalogId, string Name, string Dosage, string Frequency, string Duration, string Route, string? Instructions)> medicines)
    {
        EnsureDraft();
        SetClinicalFields(diagnosis, symptoms, clinicalObservations, advice, followUpDate);
        _items.Clear();
        var order = 0;
        foreach (var medicine in medicines)
        {
            _items.Add(PrescriptionItem.Create(
                Id,
                medicine.CatalogId,
                medicine.Name,
                medicine.Dosage,
                medicine.Frequency,
                medicine.Duration,
                medicine.Route,
                medicine.Instructions,
                order++));
        }
    }

    public void SetClinicalFields(
        string? diagnosis,
        string? symptoms,
        string? clinicalObservations,
        string? advice,
        DateOnly? followUpDate)
    {
        EnsureDraft();
        Diagnosis = Optional(diagnosis, 500);
        Symptoms = Optional(symptoms, 2000);
        ClinicalObservations = Optional(clinicalObservations, 4000);
        Advice = Optional(advice, 2000);
        FollowUpDate = followUpDate;
    }

    public void Finalize(Guid userId, DateTimeOffset now, string? pdfStorageKey)
    {
        EnsureDraft();
        if (_items.Count == 0)
        {
            throw DomainException.Validation("prescription_empty", "Add at least one medicine before finalizing.");
        }

        if (string.IsNullOrWhiteSpace(Diagnosis))
        {
            throw DomainException.Validation("prescription_diagnosis_required", "Enter a diagnosis before finalizing.");
        }

        Status = AmendedFromVersionId is null ? PrescriptionStatus.Finalized : PrescriptionStatus.Amended;
        FinalizedByUserId = userId;
        FinalizedAtUtc = now;
        PdfStorageKey = pdfStorageKey;
    }

    public void AttachPdf(string storageKey)
    {
        if (Status is not (PrescriptionStatus.Finalized or PrescriptionStatus.Amended))
        {
            throw DomainException.Conflict("prescription_not_final", "Only a finalized prescription can store a PDF.");
        }

        PdfStorageKey = storageKey;
    }

    private void EnsureDraft()
    {
        if (Status != PrescriptionStatus.Draft)
        {
            throw DomainException.Conflict("prescription_immutable", "Finalized prescriptions cannot be edited. Create an amendment instead.");
        }
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class Prescription
{
    private readonly List<PrescriptionVersion> _versions = [];

    private Prescription()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public Guid MedicalRecordId { get; private set; }

    public Guid? ConsultationRecordId { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public PrescriptionStatus Status { get; private set; }

    public int CurrentVersionNumber { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PrescriptionVersion> Versions => _versions;

    public PrescriptionVersion CurrentVersion =>
        _versions.Single(version => version.VersionNumber == CurrentVersionNumber);

    public static Prescription CreateDraft(
        string number,
        Guid medicalRecordId,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid accountUserId,
        Guid? familyMemberId,
        Guid createdByUserId,
        string patientName,
        string doctorName,
        string? doctorRegistration,
        string clinicName,
        string branchName,
        string? branchAddress,
        DateTimeOffset consultationDateUtc,
        DateTimeOffset now,
        Guid? consultationRecordId,
        Guid? appointmentId)
    {
        var prescription = new Prescription
        {
            Id = Guid.NewGuid(),
            Number = number.Trim().ToUpperInvariant(),
            MedicalRecordId = medicalRecordId,
            ConsultationRecordId = consultationRecordId,
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            AccountUserId = accountUserId,
            FamilyMemberId = familyMemberId,
            Status = PrescriptionStatus.Draft,
            CurrentVersionNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        prescription._versions.Add(PrescriptionVersion.CreateDraft(
            prescription.Id,
            1,
            createdByUserId,
            patientName,
            doctorName,
            doctorRegistration,
            clinicName,
            branchName,
            branchAddress,
            consultationDateUtc,
            now));
        return prescription;
    }

    public void UpdateDraft(
        string? diagnosis,
        string? symptoms,
        string? clinicalObservations,
        string? advice,
        DateOnly? followUpDate,
        IEnumerable<(Guid? CatalogId, string Name, string Dosage, string Frequency, string Duration, string Route, string? Instructions)> medicines,
        DateTimeOffset now)
    {
        if (Status != PrescriptionStatus.Draft)
        {
            throw DomainException.Conflict("prescription_immutable", "Finalized prescriptions cannot be edited. Create an amendment instead.");
        }

        CurrentVersion.ReplaceClinicalContent(diagnosis, symptoms, clinicalObservations, advice, followUpDate, medicines);
        UpdatedAtUtc = now;
    }

    public void UpdateDraftFields(
        string? diagnosis,
        string? symptoms,
        string? clinicalObservations,
        string? advice,
        DateOnly? followUpDate,
        DateTimeOffset now)
    {
        if (Status != PrescriptionStatus.Draft)
        {
            throw DomainException.Conflict("prescription_immutable", "Finalized prescriptions cannot be edited. Create an amendment instead.");
        }

        CurrentVersion.SetClinicalFields(diagnosis, symptoms, clinicalObservations, advice, followUpDate);
        UpdatedAtUtc = now;
    }

    public PrescriptionVersion Finalize(Guid userId, DateTimeOffset now, string? pdfStorageKey)
    {
        if (Status != PrescriptionStatus.Draft)
        {
            throw DomainException.Conflict("prescription_already_final", "This prescription is already finalized.");
        }

        CurrentVersion.Finalize(userId, now, pdfStorageKey);
        Status = CurrentVersion.Status;
        UpdatedAtUtc = now;
        return CurrentVersion;
    }

    public PrescriptionVersion BeginAmendment(Guid userId, string reason, DateTimeOffset now)
    {
        if (Status is not (PrescriptionStatus.Finalized or PrescriptionStatus.Amended))
        {
            throw DomainException.Conflict("prescription_not_final", "Only a finalized prescription can be amended.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 4)
        {
            throw DomainException.Validation("amendment_reason_required", "Explain why this prescription is being amended.");
        }

        var previous = CurrentVersion;
        var nextNumber = CurrentVersionNumber + 1;
        var draft = PrescriptionVersion.CreateDraft(
            Id,
            nextNumber,
            userId,
            previous.PatientNameSnapshot,
            previous.DoctorNameSnapshot,
            previous.DoctorRegistrationSnapshot,
            previous.ClinicNameSnapshot,
            previous.BranchNameSnapshot,
            previous.BranchAddressSnapshot,
            previous.ConsultationDateUtc,
            now,
            previous.Id,
            reason);
        draft.SetClinicalFields(
            previous.Diagnosis,
            previous.Symptoms,
            previous.ClinicalObservations,
            previous.Advice,
            previous.FollowUpDate);
        _versions.Add(draft);
        CurrentVersionNumber = nextNumber;
        Status = PrescriptionStatus.Draft;
        UpdatedAtUtc = now;
        return draft;
    }
}
