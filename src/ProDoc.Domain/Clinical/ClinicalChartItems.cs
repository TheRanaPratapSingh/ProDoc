using ProDoc.Domain.Common;

namespace ProDoc.Domain.Clinical;

public sealed class PatientAllergy
{
    private PatientAllergy()
    {
    }

    public Guid Id { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public string Substance { get; private set; } = string.Empty;

    public string? Reaction { get; private set; }

    public AllergySeverity Severity { get; private set; }

    public Guid? RecordedByDoctorId { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PatientAllergy Record(
        Guid medicalRecordId,
        string substance,
        string? reaction,
        AllergySeverity severity,
        Guid recordedByUserId,
        Guid? recordedByDoctorId,
        DateTimeOffset now)
    {
        var name = RequiredText(substance, 2, 120, "allergy_substance_invalid", "Enter the allergen.");
        return new PatientAllergy
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = medicalRecordId,
            Substance = name,
            Reaction = OptionalText(reaction, 240),
            Severity = severity,
            RecordedByUserId = recordedByUserId,
            RecordedByDoctorId = recordedByDoctorId,
            RecordedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string substance, string? reaction, AllergySeverity severity, bool isActive, DateTimeOffset now)
    {
        Substance = RequiredText(substance, 2, 120, "allergy_substance_invalid", "Enter the allergen.");
        Reaction = OptionalText(reaction, 240);
        Severity = severity;
        IsActive = isActive;
        UpdatedAtUtc = now;
    }

    private static string RequiredText(string value, int min, int max, string code, string title)
    {
        var text = value.Trim();
        if (text.Length < min || text.Length > max)
        {
            throw DomainException.Validation(code, title);
        }

        return text;
    }

    private static string? OptionalText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class PatientMedicalCondition
{
    private PatientMedicalCondition()
    {
    }

    public Guid Id { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public DateOnly? OnsetDate { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid RecordedByUserId { get; private set; }

    public Guid? RecordedByDoctorId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PatientMedicalCondition Record(
        Guid medicalRecordId,
        string name,
        string? notes,
        DateOnly? onsetDate,
        Guid recordedByUserId,
        Guid? recordedByDoctorId,
        DateTimeOffset now)
    {
        return new PatientMedicalCondition
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = medicalRecordId,
            Name = Required(name),
            Notes = Optional(notes, 500),
            OnsetDate = onsetDate,
            RecordedByUserId = recordedByUserId,
            RecordedByDoctorId = recordedByDoctorId,
            RecordedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string? notes, DateOnly? onsetDate, bool isActive, DateTimeOffset now)
    {
        Name = Required(name);
        Notes = Optional(notes, 500);
        OnsetDate = onsetDate;
        IsActive = isActive;
        UpdatedAtUtc = now;
    }

    private static string Required(string name)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("condition_name_invalid", "Enter a condition name from 2 to 160 characters.");
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

public sealed class PatientMedication
{
    private PatientMedication()
    {
    }

    public Guid Id { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Dosage { get; private set; }

    public string? Frequency { get; private set; }

    public bool IsCurrent { get; private set; } = true;

    public DateOnly? StartedOn { get; private set; }

    public DateOnly? EndedOn { get; private set; }

    public string? Notes { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public Guid? RecordedByDoctorId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PatientMedication Record(
        Guid medicalRecordId,
        string name,
        string? dosage,
        string? frequency,
        bool isCurrent,
        DateOnly? startedOn,
        DateOnly? endedOn,
        string? notes,
        Guid recordedByUserId,
        Guid? recordedByDoctorId,
        DateTimeOffset now)
    {
        return new PatientMedication
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = medicalRecordId,
            Name = Required(name),
            Dosage = Optional(dosage, 80),
            Frequency = Optional(frequency, 80),
            IsCurrent = isCurrent,
            StartedOn = startedOn,
            EndedOn = endedOn,
            Notes = Optional(notes, 500),
            RecordedByUserId = recordedByUserId,
            RecordedByDoctorId = recordedByDoctorId,
            RecordedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        string name,
        string? dosage,
        string? frequency,
        bool isCurrent,
        DateOnly? startedOn,
        DateOnly? endedOn,
        string? notes,
        DateTimeOffset now)
    {
        Name = Required(name);
        Dosage = Optional(dosage, 80);
        Frequency = Optional(frequency, 80);
        IsCurrent = isCurrent;
        StartedOn = startedOn;
        EndedOn = endedOn;
        Notes = Optional(notes, 500);
        UpdatedAtUtc = now;
    }

    private static string Required(string name)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("medication_name_invalid", "Enter a medication name from 2 to 160 characters.");
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

public sealed class MedicalDocument
{
    private MedicalDocument()
    {
    }

    public Guid Id { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static MedicalDocument Create(
        Guid medicalRecordId,
        Guid uploadedByUserId,
        string title,
        string fileName,
        string contentType,
        string storageKey,
        long sizeBytes,
        string? notes,
        DateTimeOffset now)
    {
        if (sizeBytes is <= 0 or > 10 * 1024 * 1024)
        {
            throw DomainException.Validation("document_size_invalid", "Medical files must be between 1 byte and 10 MB.");
        }

        var allowed = contentType is "application/pdf" or "image/jpeg" or "image/png" or "image/webp";
        if (!allowed)
        {
            throw DomainException.Validation("document_type_invalid", "Upload a PDF, JPG, PNG, or WebP file.");
        }

        return new MedicalDocument
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = medicalRecordId,
            UploadedByUserId = uploadedByUserId,
            Title = Required(title, 2, 160, "document_title_invalid", "Enter a document title."),
            FileName = Required(fileName, 1, 180, "document_name_invalid", "A file name is required."),
            ContentType = contentType,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
            Notes = Optional(notes, 500),
            UploadedAtUtc = now
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

public sealed class MedicineCatalogEntry
{
    private MedicineCatalogEntry()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Form { get; private set; }

    public string? StrengthHint { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static MedicineCatalogEntry Create(string name, string? form, string? strengthHint, DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("medicine_name_invalid", "Enter a medicine name from 2 to 160 characters.");
        }

        return new MedicineCatalogEntry
        {
            Id = Guid.NewGuid(),
            Name = text,
            Form = string.IsNullOrWhiteSpace(form) ? null : form.Trim()[..Math.Min(form.Trim().Length, 60)],
            StrengthHint = string.IsNullOrWhiteSpace(strengthHint)
                ? null
                : strengthHint.Trim()[..Math.Min(strengthHint.Trim().Length, 60)],
            CreatedAtUtc = now
        };
    }
}

public sealed class MedicalRecordAccessLog
{
    private MedicalRecordAccessLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public Guid? ConsultationRecordId { get; private set; }

    public Guid? PrescriptionId { get; private set; }

    public Guid? DocumentId { get; private set; }

    public MedicalAccessAction Action { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public static MedicalRecordAccessLog Record(
        Guid actorUserId,
        Guid medicalRecordId,
        MedicalAccessAction action,
        DateTimeOffset now,
        string? ipAddress,
        string? userAgent,
        Guid? consultationRecordId = null,
        Guid? prescriptionId = null,
        Guid? documentId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            MedicalRecordId = medicalRecordId,
            ConsultationRecordId = consultationRecordId,
            PrescriptionId = prescriptionId,
            DocumentId = documentId,
            Action = action,
            OccurredAtUtc = now,
            IpAddress = Trim(ipAddress, 64),
            UserAgent = Trim(userAgent, 256)
        };

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
