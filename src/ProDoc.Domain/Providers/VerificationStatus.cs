namespace ProDoc.Domain.Providers;

public enum VerificationStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    AdditionalInformationRequired = 4,
    Approved = 5,
    Rejected = 6,
    Suspended = 7,
    Expired = 8
}

public enum ReviewDecision
{
    StartReview = 1,
    RequestInformation = 2,
    Approve = 3,
    Reject = 4,
    Suspend = 5
}

public enum ClinicType
{
    IndividualPractice = 1,
    Clinic = 2,
    Polyclinic = 3,
    Hospital = 4,
    DiagnosticCentre = 5
}

public enum DocumentCategory
{
    ProfilePhoto = 1,
    MedicalDegree = 2,
    RegistrationCertificate = 3,
    QualificationCertificate = 4,
    Identity = 5,
    Other = 6,
    ClinicLogo = 7,
    ClinicRegistration = 8
}

public enum AssociationKind
{
    Independent = 1,
    Employed = 2,
    Visiting = 3,
    Hospital = 4
}
