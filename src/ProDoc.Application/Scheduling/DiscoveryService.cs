using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Application.Scheduling;

public sealed class DiscoveryService(IScheduleStore schedules, IPlaceLinkBuilder links, IClock clock)
{
    public async Task<IReadOnlyList<DiscoveryPlace>> SearchAsync(DiscoveryQuery query, CancellationToken cancellationToken)
    {
        var normalized = Normalize(query);
        var places = await schedules.SearchAsync(normalized, Today, cancellationToken);
        return places.Select(WithLink).ToArray();
    }

    public async Task<DiscoveredDoctor> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var doctor = await schedules.FindDiscoveredDoctorAsync(doctorId, Today, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "That doctor is not available.");
        return doctor with { Practices = doctor.Practices.Select(WithLink).ToArray() };
    }

    public async Task<IReadOnlyList<DiscoveredClinic>> SearchClinicsAsync(DiscoveryQuery query, CancellationToken cancellationToken)
    {
        var clinics = await schedules.SearchClinicsAsync(Normalize(query), Today, cancellationToken);
        return clinics.Select(WithClinicLinks).ToArray();
    }

    public async Task<DiscoveredClinic> GetClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        WithClinicLinks(await schedules.FindDiscoveredClinicAsync(clinicId, Today, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic is not available."));

    public Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(string? text, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? string.Empty : Collapse(text);
        if (normalized.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);
        }

        return schedules.SuggestAsync(normalized, cancellationToken);
    }

    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        clock.UtcNow,
        TimeZoneInfo.TryFindSystemTimeZoneById(ConsultationSchedule.DefaultTimeZone, out var zone) ? zone : TimeZoneInfo.Utc).DateTime);

    private DiscoveryPlace WithLink(DiscoveryPlace place)
    {
        var input = new PlaceLinkInput(
            place.ClinicName ?? place.BranchName,
            place.FormattedAddress ?? JoinAddress(place.AddressLine, place.City),
            place.Latitude,
            place.Longitude,
            place.GooglePlaceId);
        return place with
        {
            MapsUrl = links.MapsUrl(input),
            DirectionsUrl = links.DirectionsUrl(input)
        };
    }

    private DiscoveredClinic WithClinicLinks(DiscoveredClinic clinic) =>
        clinic with
        {
            Branches = clinic.Branches.Select(branch =>
            {
                var input = new PlaceLinkInput(
                    branch.Name,
                    branch.FormattedAddress ?? JoinAddress(branch.AddressLine, branch.City),
                    branch.Latitude,
                    branch.Longitude,
                    branch.GooglePlaceId);
                return branch with
                {
                    MapsUrl = links.MapsUrl(input),
                    DirectionsUrl = links.DirectionsUrl(input)
                };
            }).ToArray()
        };

    private static string? JoinAddress(string? line, string? city)
    {
        var joined = string.Join(", ", new[] { line, city }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static DiscoveryQuery Normalize(DiscoveryQuery query)
    {
        if (query.RadiusKm is < 1 or > 100)
        {
            throw DomainException.Validation("radius_invalid", "Choose a distance from 1 to 100 kilometres.");
        }

        if ((query.Latitude is null) != (query.Longitude is null))
        {
            throw DomainException.Validation("location_invalid", "Enter both latitude and longitude.");
        }

        if (query.Latitude is < -90 or > 90 || query.Longitude is < -180 or > 180)
        {
            throw DomainException.Validation("location_invalid", "Enter a valid latitude and longitude.");
        }

        if (query.MinFee is < 0 || query.MaxFee is < 0 || (query.MinFee is decimal min && query.MaxFee is decimal max && max < min))
        {
            throw DomainException.Validation("fee_invalid", "Enter a consultation fee range in INR.");
        }

        var availability = query.Availability?.Trim().ToLowerInvariant();
        if (availability is not null && availability is not ("today" or "tomorrow" or "week"))
        {
            throw DomainException.Validation("availability_invalid", "Choose today, tomorrow, or this week.");
        }

        var sort = query.Sort?.Trim().ToLowerInvariant();
        if (sort is not null && sort is not ("recommended" or "nearest" or "fee" or "experience" or "earliest"))
        {
            throw DomainException.Validation("sort_invalid", "Choose a supported sort order.");
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;
        return query with
        {
            Text = Blank(query.Text),
            City = Blank(query.City),
            Gender = Blank(query.Gender)?.ToLowerInvariant(),
            Language = Blank(query.Language)?.ToLowerInvariant(),
            Qualification = Blank(query.Qualification),
            PostalCode = Blank(query.PostalCode),
            Locality = Blank(query.Locality),
            Availability = availability,
            Sort = sort,
            ClinicType = Blank(query.ClinicType),
            RadiusKm = query.RadiusKm ?? (query.Latitude is null ? null : 25),
            Page = page,
            PageSize = pageSize
        };
    }

    private static string? Blank(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Collapse(value);
    }

    private static string Collapse(string value)
    {
        var parts = value.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }
}
