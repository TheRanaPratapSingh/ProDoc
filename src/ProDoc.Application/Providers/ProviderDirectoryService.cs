using ProDoc.Application.Abstractions;

namespace ProDoc.Application.Providers;

public sealed class ProviderDirectoryService(IProviderStore store, IClock clock)
{
    public async Task<IReadOnlyList<SpecializationModel>> ListSpecializationsAsync(CancellationToken cancellationToken)
    {
        var items = await store.ListSpecializationsAsync(cancellationToken);
        return items
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new SpecializationModel(item.Id, item.Code, item.Name, false))
            .ToArray();
    }

    public Task<IReadOnlyList<DoctorCard>> SearchDoctorsAsync(
        string? query,
        Guid? specializationId,
        CancellationToken cancellationToken) =>
        store.SearchDoctorsAsync(Normalize(query), specializationId, Today, cancellationToken);

    public async Task<PublicDoctor> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        return await store.FindPublicDoctorAsync(doctorId, Today, cancellationToken)
            ?? throw Domain.Common.DomainException.NotFound("doctor_not_found", "That doctor is not available.");
    }

    public Task<IReadOnlyList<ClinicCard>> SearchClinicsAsync(string? query, CancellationToken cancellationToken) =>
        store.SearchClinicsAsync(Normalize(query), Today, cancellationToken);

    public async Task<PublicClinic> GetClinicAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        return await store.FindPublicClinicAsync(clinicId, Today, cancellationToken)
            ?? throw Domain.Common.DomainException.NotFound("clinic_not_found", "That clinic is not available.");
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private static string? Normalize(string? query)
    {
        var trimmed = query?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
