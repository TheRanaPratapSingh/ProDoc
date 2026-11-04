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
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string TimeZoneId,
    bool IsPublished,
    bool RequiresApproval,
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
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    decimal ConsultationFee,
    int SlotDurationMinutes,
    double? DistanceKm,
    string? PostalCode,
    string? PhoneNumber,
    string? DirectionsUrl);

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
    string? Locality,
    string City,
    string State,
    string PostalCode,
    string? PhoneNumber,
    decimal? Latitude,
    decimal? Longitude,
    double? DistanceKm,
    int DoctorCount,
    string? DirectionsUrl,
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

public sealed record AvailableSlot(TimeOnly Start, TimeOnly End);

public sealed record SlotAvailability(Guid ScheduleId, DateOnly Date, IReadOnlyList<AvailableSlot> Slots);

public sealed record DayAvailability(DateOnly Date, string Status, int SlotCount, TimeOnly? FirstSlot, Guid? ScheduleId);

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
