using ProDoc.Domain.Common;

namespace ProDoc.Domain.Clinical;

public sealed class ConsultationDiagnosis
{
    private ConsultationDiagnosis()
    {
    }

    public Guid Id { get; private set; }

    public Guid ConsultationRecordId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public bool IsPrimary { get; private set; }

    public int SortOrder { get; private set; }

    public static ConsultationDiagnosis Create(Guid consultationRecordId, string description, string? code, bool isPrimary, int sortOrder)
    {
        var text = description.Trim();
        if (text.Length is < 2 or > 240)
        {
            throw DomainException.Validation("diagnosis_invalid", "Enter a diagnosis from 2 to 240 characters.");
        }

        return new ConsultationDiagnosis
        {
            Id = Guid.NewGuid(),
            ConsultationRecordId = consultationRecordId,
            Description = text,
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim()[..Math.Min(code.Trim().Length, 32)],
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };
    }
}

public sealed class ConsultationRecord
{
    private readonly List<ConsultationDiagnosis> _diagnoses = [];

    private ConsultationRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid MedicalRecordId { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? QueueEntryId { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    public string? Symptoms { get; private set; }

    public string? ClinicalObservations { get; private set; }

    public string? DoctorNotes { get; private set; }

    public string? Advice { get; private set; }

    public DateOnly? FollowUpDate { get; private set; }

    public ConsultationStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<ConsultationDiagnosis> Diagnoses => _diagnoses;

    public static ConsultationRecord Start(
        Guid medicalRecordId,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid accountUserId,
        Guid? familyMemberId,
        string patientName,
        Guid? appointmentId,
        Guid? queueEntryId,
        DateTimeOffset now)
    {
        var name = patientName.Trim();
        if (name.Length is < 2 or > 120)
        {
            throw DomainException.Validation("patient_name_invalid", "Enter a patient name from 2 to 120 characters.");
        }

        return new ConsultationRecord
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = medicalRecordId,
            AppointmentId = appointmentId,
            QueueEntryId = queueEntryId,
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            AccountUserId = accountUserId,
            FamilyMemberId = familyMemberId,
            PatientName = name,
            Status = ConsultationStatus.InProgress,
            StartedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateNotes(
        string? symptoms,
        string? clinicalObservations,
        string? doctorNotes,
        string? advice,
        DateOnly? followUpDate,
        DateTimeOffset now)
    {
        EnsureEditable();
        Symptoms = Optional(symptoms, 2000);
        ClinicalObservations = Optional(clinicalObservations, 4000);
        DoctorNotes = Optional(doctorNotes, 4000);
        Advice = Optional(advice, 2000);
        FollowUpDate = followUpDate;
        UpdatedAtUtc = now;
    }

    public void ReplaceDiagnoses(IEnumerable<(string Description, string? Code, bool IsPrimary)> diagnoses)
    {
        EnsureEditable();
        _diagnoses.Clear();
        var order = 0;
        foreach (var item in diagnoses)
        {
            _diagnoses.Add(ConsultationDiagnosis.Create(Id, item.Description, item.Code, item.IsPrimary, order++));
        }
    }

    public void Complete(DateTimeOffset now, bool hasDiagnoses)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(DoctorNotes)
            && string.IsNullOrWhiteSpace(ClinicalObservations)
            && !hasDiagnoses)
        {
            throw DomainException.Validation(
                "consultation_incomplete",
                "Add a diagnosis or clinical notes before completing the consultation.");
        }

        Status = ConsultationStatus.Completed;
        CompletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    private void EnsureEditable()
    {
        if (Status != ConsultationStatus.InProgress)
        {
            throw DomainException.Conflict("consultation_locked", "This consultation is already completed.");
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
