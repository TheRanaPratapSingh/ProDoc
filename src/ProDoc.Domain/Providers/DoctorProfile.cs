namespace ProDoc.Domain.Providers;

public sealed class DoctorQualification
{
    private DoctorQualification()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Institution { get; private set; }

    public int GraduationYear { get; private set; }

    internal static DoctorQualification Create(Guid doctorId, string name, string? institution, int graduationYear)
    {
        return new DoctorQualification
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Name = name,
            Institution = institution,
            GraduationYear = graduationYear
        };
    }
}

public sealed class DoctorSpecialization
{
    private DoctorSpecialization()
    {
    }

    public Guid DoctorId { get; private set; }

    public Guid SpecializationId { get; private set; }

    public bool IsPrimary { get; private set; }

    internal static DoctorSpecialization Create(Guid doctorId, Guid specializationId, bool isPrimary)
    {
        return new DoctorSpecialization
        {
            DoctorId = doctorId,
            SpecializationId = specializationId,
            IsPrimary = isPrimary
        };
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}

public sealed class DoctorSpokenLanguage
{
    private DoctorSpokenLanguage()
    {
    }

    public Guid DoctorId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    internal static DoctorSpokenLanguage Create(Guid doctorId, string code) =>
        new() { DoctorId = doctorId, Code = code };
}

public sealed class DoctorExpertise
{
    private DoctorExpertise()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    internal static DoctorExpertise Create(Guid doctorId, string label) =>
        new() { Id = Guid.NewGuid(), DoctorId = doctorId, Label = label };
}

public sealed class DoctorRegistration
{
    private DoctorRegistration()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Council { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string RegistrationType { get; private set; } = string.Empty;

    public DateOnly RegisteredOn { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public int Version { get; private set; }

    public bool IsCurrent { get; private set; }

    internal static DoctorRegistration Create(
        Guid doctorId,
        string number,
        string council,
        string state,
        string registrationType,
        DateOnly registeredOn,
        DateOnly? expiresOn,
        int version)
    {
        return new DoctorRegistration
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Number = number,
            Council = council,
            State = state,
            RegistrationType = registrationType,
            RegisteredOn = registeredOn,
            ExpiresOn = expiresOn,
            Version = version,
            IsCurrent = true
        };
    }

    internal bool Matches(
        string number,
        string council,
        string state,
        string registrationType,
        DateOnly registeredOn,
        DateOnly? expiresOn) =>
        string.Equals(Number, number, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Council, council, StringComparison.OrdinalIgnoreCase)
        && string.Equals(State, state, StringComparison.OrdinalIgnoreCase)
        && RegistrationType == registrationType
        && RegisteredOn == registeredOn
        && ExpiresOn == expiresOn;

    internal void Supersede() => IsCurrent = false;

    internal bool IsValidOn(DateOnly today) => ExpiresOn is null || ExpiresOn >= today;
}
