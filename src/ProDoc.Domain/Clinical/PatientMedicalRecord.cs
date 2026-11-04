using ProDoc.Domain.Common;

namespace ProDoc.Domain.Clinical;

public sealed class PatientMedicalRecord
{
    private readonly List<PatientAllergy> _allergies = [];
    private readonly List<PatientMedicalCondition> _conditions = [];
    private readonly List<PatientMedication> _medications = [];
    private readonly List<MedicalDocument> _documents = [];
    private readonly List<ConsultationRecord> _consultations = [];
    private readonly List<Prescription> _prescriptions = [];

    private PatientMedicalRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public string SubjectName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PatientAllergy> Allergies => _allergies;

    public IReadOnlyCollection<PatientMedicalCondition> Conditions => _conditions;

    public IReadOnlyCollection<PatientMedication> Medications => _medications;

    public IReadOnlyCollection<MedicalDocument> Documents => _documents;

    public IReadOnlyCollection<ConsultationRecord> Consultations => _consultations;

    public IReadOnlyCollection<Prescription> Prescriptions => _prescriptions;

    public static PatientMedicalRecord Open(
        Guid accountUserId,
        Guid? familyMemberId,
        string subjectName,
        DateTimeOffset now)
    {
        if (accountUserId == Guid.Empty)
        {
            throw DomainException.Validation("patient_required", "A patient account is required.");
        }

        var name = subjectName.Trim();
        if (name.Length is < 2 or > 120)
        {
            throw DomainException.Validation("patient_name_invalid", "Enter a patient name from 2 to 120 characters.");
        }

        return new PatientMedicalRecord
        {
            Id = Guid.NewGuid(),
            AccountUserId = accountUserId,
            FamilyMemberId = familyMemberId,
            SubjectName = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Rename(string subjectName, DateTimeOffset now)
    {
        var name = subjectName.Trim();
        if (name.Length is < 2 or > 120)
        {
            throw DomainException.Validation("patient_name_invalid", "Enter a patient name from 2 to 120 characters.");
        }

        SubjectName = name;
        UpdatedAtUtc = now;
    }

    public void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
