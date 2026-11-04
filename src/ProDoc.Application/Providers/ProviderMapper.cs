using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public static class ProviderMapper
{
    public static DoctorProfile ToDoctor(
        Doctor doctor,
        string? phoneNumber,
        IReadOnlyDictionary<Guid, Specialization> specializations,
        IReadOnlyList<AffiliationModel> affiliations,
        DateOnly today) =>
        new(
            doctor.Id,
            doctor.Reference,
            doctor.Status.ToString(),
            doctor.IsPubliclyBookable(today),
            doctor.IsAcceptingPatients,
            doctor.FirstName,
            doctor.LastName,
            phoneNumber,
            doctor.DateOfBirth,
            doctor.Gender,
            doctor.Email,
            doctor.PreferredLanguage,
            doctor.DisplayName,
            doctor.Biography,
            doctor.YearsOfExperience,
            doctor.Languages.Select(language => language.Code).Order(StringComparer.Ordinal).ToArray(),
            doctor.Expertise.Select(item => item.Label).ToArray(),
            doctor.Qualifications
                .OrderBy(item => item.Name)
                .Select(item => new QualificationModel(item.Id, item.Name, item.Institution, item.GraduationYear))
                .ToArray(),
            doctor.Specializations
                .Select(link =>
                {
                    specializations.TryGetValue(link.SpecializationId, out var specialization);
                    return new SpecializationModel(
                        link.SpecializationId,
                        specialization?.Code ?? string.Empty,
                        specialization?.Name ?? string.Empty,
                        link.IsPrimary);
                })
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToArray(),
            doctor.Registrations
                .OrderByDescending(item => item.Version)
                .Select(item => new RegistrationModel(
                    item.Id,
                    item.Number,
                    item.Council,
                    item.State,
                    item.RegistrationType,
                    item.RegisteredOn,
                    item.ExpiresOn,
                    item.Version,
                    item.IsCurrent))
                .ToArray(),
            doctor.Documents.OrderByDescending(document => document.UploadedAtUtc).Select(ToDocument).ToArray(),
            affiliations,
            doctor.RejectionReason,
            doctor.SubmittedAtUtc,
            doctor.ReviewedAtUtc);

    public static ClinicProfile ToClinic(Clinic clinic, DateOnly today) =>
        new(
            clinic.Id,
            clinic.Reference,
            clinic.Status.ToString(),
            clinic.IsPubliclyListed(today),
            clinic.Name,
            clinic.Type?.ToString(),
            clinic.LegalName,
            clinic.Description,
            clinic.PhoneNumber,
            clinic.Email,
            clinic.Website,
            clinic.EstablishmentYear,
            clinic.RegistrationNumber,
            clinic.RegistrationAuthority,
            clinic.RegistrationState,
            clinic.RegistrationExpiresOn,
            clinic.Branches
                .OrderByDescending(branch => branch.IsPrimary)
                .ThenBy(branch => branch.Name, StringComparer.Ordinal)
                .Select(ToBranch)
                .ToArray(),
            clinic.Documents.OrderByDescending(document => document.UploadedAtUtc).Select(ToDocument).ToArray(),
            clinic.RejectionReason,
            clinic.SubmittedAtUtc,
            clinic.ReviewedAtUtc);

    public static DocumentModel ToDocument(ProviderDocument document) =>
        new(
            document.Id,
            document.Category.ToString(),
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Version,
            document.IsCurrent,
            document.SupersedesDocumentId,
            document.ScanEngine,
            document.ScanDetail,
            document.RejectionReason,
            document.UploadedAtUtc);

    public static ClinicBranchModel ToBranch(ClinicBranch branch) =>
        new(
            branch.Id,
            branch.Name,
            branch.AddressLine,
            branch.AddressLine2,
            branch.City,
            branch.State,
            branch.PostalCode,
            branch.Latitude,
            branch.Longitude,
            branch.PhoneNumber,
            branch.IsPrimary,
            branch.IsActive);
}
