namespace ProDoc.Application.Scheduling;

public sealed record HoursModel(Guid Id, DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt);

public sealed record BreakModel(Guid Id, DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt);

public sealed record ClosureModel(Guid Id, DateOnly Date, string? Reason);

public sealed record ExtraModel(Guid Id, DateOnly Date, TimeOnly StartsAt, TimeOnly EndsAt);

public sealed record ScheduleDetails(
    Guid Id,
    Guid DoctorId,
    Guid? ClinicId,
    Guid? BranchId,
    decimal ConsultationFee,
    string Currency,
    int SlotDurationMinutes,
    int BufferMinutes,
    int? MaxPatients,
    string BookingMode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string TimeZoneId,
    bool IsPublished,
    bool RequiresApproval,
    string RowVersion,
    IReadOnlyList<HoursModel> Hours,
    IReadOnlyList<BreakModel> Breaks,
    IReadOnlyList<ClosureModel> Closures,
    IReadOnlyList<ExtraModel> Extras);

public sealed record DiscoveryPlace(
    Guid ScheduleId,
    Guid DoctorId,
    string DisplayName,
    string PrimarySpecialization,
    int YearsOfExperience,
    Guid? PhotoDocumentId,
    Guid? ClinicId,
    string? ClinicName,
    Guid? BranchId,
    string? BranchName,
    string? AddressLine,
    string? AddressLine2,
    string? Landmark,
    string? Locality,
    string? City,
    string? District,
    string? State,
    string? PostalCode,
    string? Country,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? FormattedAddress,
    string? CompactAddress,
    decimal ConsultationFee,
    int SlotDurationMinutes,
    double? DistanceKm,
    string? PhoneNumber,
    string? Qualification,
    string? RegistrationNumber,
    string? RegistrationCouncil,
    string? MapsUrl,
    string? DirectionsUrl,
    bool LocationVerified);

public sealed record DiscoveredDoctor(
    Guid Id,
    string DisplayName,
    string? Biography,
    int YearsOfExperience,
    string? Gender,
    string PrimarySpecialization,
    IReadOnlyList<string> Specializations,
    IReadOnlyList<string> Qualifications,
    IReadOnlyList<string> Expertise,
    IReadOnlyList<string> Languages,
    string? RegistrationCouncil,
    string? RegistrationState,
    Guid? PhotoDocumentId,
    IReadOnlyList<DiscoveryPlace> Practices);

public sealed record DiscoveredBranch(
    Guid Id,
    string Name,
    string AddressLine,
    string? AddressLine2,
    string? Landmark,
    string? Locality,
    string City,
    string? District,
    string State,
    string PostalCode,
    string Country,
    string? PhoneNumber,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? FormattedAddress,
    string? CompactAddress,
    double? DistanceKm,
    int DoctorCount,
    string? MapsUrl,
    string? DirectionsUrl,
    bool LocationVerified,
    bool OpenToday);

public sealed record DiscoveredClinic(
    Guid Id,
    string Name,
    string Type,
    string? Description,
    Guid? LogoDocumentId,
    IReadOnlyList<string> Facilities,
    IReadOnlyList<string> Specializations,
    IReadOnlyList<DiscoveredBranch> Branches);

public sealed record SearchSuggestion(string Kind, string Label, Guid? Id);

public sealed record AvailableSlot(
    TimeOnly Start,
    TimeOnly End,
    bool Available = true,
    int? BookedCount = null,
    int? Capacity = null,
    int? Remaining = null,
    string? TokenPrefix = null);

public sealed record SlotAvailability(
    Guid ScheduleId,
    DateOnly Date,
    string BookingMode,
    IReadOnlyList<AvailableSlot> Slots);

public sealed record DayAvailability(DateOnly Date, string Status, int SlotCount, TimeOnly? FirstSlot, Guid? ScheduleId);

public sealed record ClosureImpact(int ConfirmedAppointments);

/// <summary>
/// Patient booking sees only the clinic week containing today plus the next week (14 days, Mon–Sun).
/// Staff / doctor management may look further ahead.
/// </summary>
public enum AvailabilityHorizon
{
    PatientTwoWeeks = 0,
    StaffExtended = 1,
}

public sealed record DiscoveryQuery(
    string? Text,
    Guid? SpecializationId,
    Guid? ClinicId,
    string? City,
    double? Latitude,
    double? Longitude,
    double? RadiusKm,
    decimal? MinFee,
    decimal? MaxFee,
    int? MinExperience,
    string? Gender,
    string? Language,
    string? Qualification,
    string? PostalCode,
    string? Locality,
    string? Availability,
    string? Sort,
    string? ClinicType,
    int Page,
    int PageSize);

public sealed record OperatingHourModel(Guid Id, DayOfWeek Day, TimeOnly OpensAt, TimeOnly ClosesAt, bool IsClosed);

public sealed record BranchOperations(
    Guid BranchId,
    IReadOnlyList<OperatingHourModel> Hours,
    IReadOnlyList<ClosureModel> Closures);
