using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public static class DoctorProfileCompletion
{
    public static string VerificationBadge(VerificationStatus status) =>
        status switch
        {
            VerificationStatus.Approved => "Verified",
            VerificationStatus.Rejected => "Rejected",
            _ => "Pending",
        };

    public static Guid? PhotoDocumentId(Doctor doctor) =>
        doctor.Documents
            .Where(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto)
            .OrderByDescending(document => document.UploadedAtUtc)
            .Select(document => (Guid?)document.Id)
            .FirstOrDefault();

    public static int Percent(Doctor doctor, DateOnly today)
    {
        var checks = new[]
        {
            !string.IsNullOrWhiteSpace(doctor.FirstName) && !string.IsNullOrWhiteSpace(doctor.LastName),
            doctor.DateOfBirth is not null,
            !string.IsNullOrWhiteSpace(doctor.Gender),
            !string.IsNullOrWhiteSpace(doctor.DisplayName),
            doctor.YearsOfExperience >= 0,
            doctor.Languages.Count > 0,
            doctor.Qualifications.Count > 0,
            doctor.Specializations.Any(link => link.IsPrimary),
            doctor.HasValidRegistration(today),
            doctor.Documents.Any(document => document.IsCurrent && document.Category == DocumentCategory.RegistrationCertificate),
            doctor.Documents.Any(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto),
        };

        var done = checks.Count(item => item);
        return (int)Math.Round(100d * done / checks.Length, MidpointRounding.AwayFromZero);
    }
}
