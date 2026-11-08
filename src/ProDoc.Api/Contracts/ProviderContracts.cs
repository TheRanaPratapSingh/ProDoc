using FluentValidation;
using ProDoc.Api.Validation;

namespace ProDoc.Api.Contracts;

public sealed class DoctorBasicsRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? Email { get; set; }

    public string PreferredLanguage { get; set; } = "en";
}

public sealed class DoctorBasicsRequestValidator : AbstractValidator<DoctorBasicsRequest>
{
    public DoctorBasicsRequestValidator()
    {
        RuleFor(request => request.FirstName).MaximumLength(40);
        RuleFor(request => request.LastName).MaximumLength(40);
        RuleFor(request => request.Gender).MaximumLength(20);
        RuleFor(request => request.Email).MaximumLength(160);
        RuleFor(request => request.PreferredLanguage).Must(language => language is "en" or "hi");
    }
}

public sealed class DoctorProfessionalRequest
{
    public string? DisplayName { get; set; }

    public string? Biography { get; set; }

    public int YearsOfExperience { get; set; }

    public string[] Languages { get; set; } = [];

    public string[] Expertise { get; set; } = [];
}

public sealed class DoctorProfessionalRequestValidator : AbstractValidator<DoctorProfessionalRequest>
{
    public DoctorProfessionalRequestValidator()
    {
        RuleFor(request => request.DisplayName).MaximumLength(120);
        RuleFor(request => request.Biography).MaximumLength(2000);
        RuleFor(request => request.YearsOfExperience)
            .InclusiveBetween(0, 70)
            .WithMessage("Years of experience must be a whole number between 0 and 70.");
        RuleFor(request => request.Languages).Must(languages => languages.Length <= 13);
        RuleFor(request => request.Expertise).Must(items => items.Length <= 8);
    }
}

public sealed class QualificationRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Institution { get; set; }

    public int GraduationYear { get; set; }
}

public sealed class QualificationRequestValidator : AbstractValidator<QualificationRequest>
{
    public QualificationRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Institution).MaximumLength(160);
    }
}

public sealed class SpecializationsRequest
{
    public Guid[] SpecializationIds { get; set; } = [];

    public Guid PrimarySpecializationId { get; set; }
}

public sealed class SpecializationsRequestValidator : AbstractValidator<SpecializationsRequest>
{
    public SpecializationsRequestValidator()
    {
        RuleFor(request => request.SpecializationIds).NotEmpty().Must(ids => ids.Length <= 12);
        RuleFor(request => request.PrimarySpecializationId).NotEmpty();
    }
}

public sealed class RegistrationRequest
{
    public string Number { get; set; } = string.Empty;

    public string Council { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string RegistrationType { get; set; } = string.Empty;

    public DateOnly RegisteredOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }
}

public sealed class RegistrationRequestValidator : AbstractValidator<RegistrationRequest>
{
    public RegistrationRequestValidator()
    {
        RuleFor(request => request.Number).NotEmpty().MaximumLength(40);
        RuleFor(request => request.Council).NotEmpty().MaximumLength(120);
        RuleFor(request => request.State).NotEmpty().MaximumLength(80);
        RuleFor(request => request.RegistrationType).NotEmpty().MaximumLength(20);
    }
}

public sealed class AvailabilityRequest
{
    public bool IsAcceptingPatients { get; set; }
}

public sealed class AffiliationRequest
{
    public Guid ClinicId { get; set; }

    public Guid? BranchId { get; set; }

    public string Kind { get; set; } = string.Empty;
}

public sealed class AffiliationRequestValidator : AbstractValidator<AffiliationRequest>
{
    public AffiliationRequestValidator()
    {
        RuleFor(request => request.ClinicId).NotEmpty();
        RuleFor(request => request.Kind).NotEmpty().MaximumLength(40);
    }
}

public sealed class ClinicDetailsRequest
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? LegalName { get; set; }

    public string? Description { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public int? EstablishmentYear { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? RegistrationAuthority { get; set; }

    public string? RegistrationState { get; set; }

    public DateOnly? RegistrationExpiresOn { get; set; }
}

public sealed class ClinicDetailsRequestValidator : AbstractValidator<ClinicDetailsRequest>
{
    public ClinicDetailsRequestValidator()
    {
        RuleFor(request => request.Name).MaximumLength(160);
        RuleFor(request => request.Type).MaximumLength(40);
        RuleFor(request => request.LegalName).MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.PhoneNumber).MaximumLength(20).MustBeIndianMobileWhenPresent();
        RuleFor(request => request.Email).MaximumLength(160).EmailAddress().When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.Website).MaximumLength(200);
        RuleFor(request => request.RegistrationNumber).MaximumLength(40);
        RuleFor(request => request.RegistrationAuthority).MaximumLength(120);
        RuleFor(request => request.RegistrationState).MaximumLength(80);
    }
}

public sealed class BranchRequest
{
    public string Name { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string? Landmark { get; set; }

    public string? Locality { get; set; }

    public string City { get; set; } = string.Empty;

    public string? District { get; set; }

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string? Country { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? GooglePlaceId { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class BranchRequestValidator : AbstractValidator<BranchRequest>
{
    public BranchRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.AddressLine).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AddressLine2).MaximumLength(200);
        RuleFor(request => request.Landmark).MaximumLength(160);
        RuleFor(request => request.Locality).MaximumLength(120);
        RuleFor(request => request.City).NotEmpty().MaximumLength(80);
        RuleFor(request => request.District).MaximumLength(80);
        RuleFor(request => request.State).NotEmpty().MaximumLength(80);
        RuleFor(request => request.PostalCode).NotEmpty().Matches(@"^\d{6}$").WithMessage("Enter a valid 6-digit PIN code.");
        RuleFor(request => request.Country).MaximumLength(80);
        RuleFor(request => request.GooglePlaceId).MaximumLength(256);
        RuleFor(request => request.PhoneNumber).MaximumLength(20).MustBeIndianMobileWhenPresent();
        RuleFor(request => request.Latitude)
            .InclusiveBetween(-90, 90)
            .When(request => request.Latitude is not null);
        RuleFor(request => request.Longitude)
            .InclusiveBetween(-180, 180)
            .When(request => request.Longitude is not null);
        RuleFor(request => request)
            .Must(request => (request.Latitude is null) == (request.Longitude is null))
            .WithMessage("Enter both latitude and longitude, or leave both empty.");
    }
}

public sealed class PracticeLocationRequest
{
    public string AddressLine { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string? Landmark { get; set; }

    public string? Locality { get; set; }

    public string City { get; set; } = string.Empty;

    public string? District { get; set; }

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string? Country { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? GooglePlaceId { get; set; }

    public bool ClearOverride { get; set; }
}

public sealed class PracticeLocationRequestValidator : AbstractValidator<PracticeLocationRequest>
{
    public PracticeLocationRequestValidator()
    {
        When(request => !request.ClearOverride, () =>
        {
            RuleFor(request => request.AddressLine).NotEmpty().MaximumLength(200);
            RuleFor(request => request.AddressLine2).MaximumLength(200);
            RuleFor(request => request.Landmark).MaximumLength(160);
            RuleFor(request => request.Locality).MaximumLength(120);
            RuleFor(request => request.City).NotEmpty().MaximumLength(80);
            RuleFor(request => request.District).MaximumLength(80);
            RuleFor(request => request.State).NotEmpty().MaximumLength(80);
            RuleFor(request => request.PostalCode).NotEmpty().Matches(@"^\d{6}$");
            RuleFor(request => request.Country).MaximumLength(80);
            RuleFor(request => request.GooglePlaceId).MaximumLength(256);
            RuleFor(request => request)
                .Must(request => (request.Latitude is null) == (request.Longitude is null))
                .WithMessage("Enter both latitude and longitude, or leave both empty.");
        });
    }
}

public sealed class CreateScheduleRequest
{
    public Guid? DoctorId { get; set; }

    public Guid? ClinicId { get; set; }

    public Guid? BranchId { get; set; }

    public decimal ConsultationFee { get; set; }

    public int SlotDurationMinutes { get; set; } = 10;

    public int BufferMinutes { get; set; }
}

public sealed class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(request => request.ConsultationFee).InclusiveBetween(0, 100_000);
        RuleFor(request => request.SlotDurationMinutes).InclusiveBetween(5, 120);
        RuleFor(request => request.BufferMinutes).InclusiveBetween(0, 60);
    }
}

public sealed class UpdateScheduleRequest
{
    public decimal ConsultationFee { get; set; }

    public int SlotDurationMinutes { get; set; }

    public int BufferMinutes { get; set; }

    public int? MaxPatients { get; set; }

    /// <summary>TimedSlots or HourlyQueue. Omit to leave unchanged.</summary>
    public string? BookingMode { get; set; }

    /// <summary>Required when switching modes while future bookings exist.</summary>
    public bool ConfirmBookingModeChange { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string? TimeZoneId { get; set; }

    public bool RequiresApproval { get; set; }

    public string? ExpectedRowVersion { get; set; }
}

public sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(request => request.ConsultationFee).InclusiveBetween(0, 100_000);
        RuleFor(request => request.SlotDurationMinutes).InclusiveBetween(5, 120);
        RuleFor(request => request.BufferMinutes).InclusiveBetween(0, 60);
        RuleFor(request => request.MaxPatients).InclusiveBetween(1, 200);
        RuleFor(request => request.TimeZoneId).MaximumLength(64);
        RuleFor(request => request.BookingMode)
            .Must(value => value is null
                || value.Equals("TimedSlots", StringComparison.OrdinalIgnoreCase)
                || value.Equals("HourlyQueue", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Choose TimedSlots or HourlyQueue.");
    }
}

public sealed class ExtraRequest
{
    public DateOnly Date { get; set; }

    public string StartsAt { get; set; } = string.Empty;

    public string EndsAt { get; set; } = string.Empty;

    public string? ExpectedRowVersion { get; set; }
}

public sealed class DayWindowRequest
{
    public string StartsAt { get; set; } = string.Empty;

    public string EndsAt { get; set; } = string.Empty;
}

public sealed class ReplaceDayOverrideRequest
{
    public DateOnly Date { get; set; }

    public bool Available { get; set; } = true;

    public string? UnavailableReason { get; set; }

    public DayWindowRequest[] Windows { get; set; } = [];

    public string? ExpectedRowVersion { get; set; }
}

public sealed class ResetDayOverrideRequest
{
    public DateOnly Date { get; set; }

    public string? ExpectedRowVersion { get; set; }
}

public sealed class CopyDayScheduleRequest
{
    public DateOnly SourceDate { get; set; }

    public DateOnly[] TargetDates { get; set; } = [];

    public bool OverwriteExisting { get; set; }

    public string? ExpectedRowVersion { get; set; }
}

public sealed class OperatingHoursRequest
{
    public OperatingPeriodRequest[] Periods { get; set; } = [];
}

public sealed class OperatingPeriodRequest
{
    public string Day { get; set; } = string.Empty;

    public string OpensAt { get; set; } = "09:00";

    public string ClosesAt { get; set; } = "17:00";

    public bool IsClosed { get; set; }
}

public sealed class FacilityRequest
{
    public string[] Names { get; set; } = [];
}

public sealed class PeriodsRequest
{
    public PeriodRequest[] Periods { get; set; } = [];

    public string? ExpectedRowVersion { get; set; }
}

public sealed class PeriodRequest
{
    public string Day { get; set; } = string.Empty;

    public string StartsAt { get; set; } = string.Empty;

    public string EndsAt { get; set; } = string.Empty;
}

public sealed class PeriodsRequestValidator : AbstractValidator<PeriodsRequest>
{
    public PeriodsRequestValidator()
    {
        RuleFor(request => request.Periods).NotNull();
        RuleForEach(request => request.Periods).ChildRules(period =>
        {
            period.RuleFor(item => item.Day).NotEmpty().MaximumLength(12);
            period.RuleFor(item => item.StartsAt).NotEmpty().MaximumLength(8);
            period.RuleFor(item => item.EndsAt).NotEmpty().MaximumLength(8);
        });
    }
}

public sealed class ClosureRequest
{
    public DateOnly Date { get; set; }

    public string? Reason { get; set; }

    public string? ExpectedRowVersion { get; set; }
}

public sealed class ClosureRequestValidator : AbstractValidator<ClosureRequest>
{
    public ClosureRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(120);
    }
}

public sealed class PublicationRequest
{
    public bool IsPublished { get; set; }

    public string? ExpectedRowVersion { get; set; }
}

public sealed class ReviewRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class ReviewRequestValidator : AbstractValidator<ReviewRequest>
{
    public ReviewRequestValidator()
    {
        RuleFor(request => request.Decision).NotEmpty().MaximumLength(40);
        RuleFor(request => request.Reason).MaximumLength(500);
    }
}
