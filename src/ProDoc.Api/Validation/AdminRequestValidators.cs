using FluentValidation;
using ProDoc.Api.Controllers;

namespace ProDoc.Api.Validation;

public sealed class InviteStaffRequestValidator : AbstractValidator<InviteStaffRequest>
{
    public InviteStaffRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(80);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).MustBeIndianMobile();
        RuleFor(request => request.Email)
            .MaximumLength(160)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.RoleName).NotEmpty().MaximumLength(40);
        RuleFor(request => request.YearsOfExperience)
            .NotNull()
            .InclusiveBetween(0, 70)
            .When(request => string.Equals(request.RoleName?.Trim(), "Doctor", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Years of experience must be a whole number between 0 and 70.");
        RuleFor(request => request.Qualifications).MaximumLength(200);
        RuleFor(request => request.RegistrationNumber).MaximumLength(40);
        RuleFor(request => request.Designation).MaximumLength(80);
    }
}

public sealed class VerifyStaffInvitationRequestValidator : AbstractValidator<VerifyStaffInvitationRequest>
{
    public VerifyStaffInvitationRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Enter the 6-digit code.");
    }
}

public sealed class CreateAdminClinicRequestValidator : AbstractValidator<CreateAdminClinicRequest>
{
    public CreateAdminClinicRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Type).NotEmpty().MaximumLength(40);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).MustBeIndianMobile();
        RuleFor(request => request.Email)
            .MaximumLength(160)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.Website).MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.RegistrationNumber).MaximumLength(40);
        RuleFor(request => request.BranchName).MaximumLength(120);
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
        RuleFor(request => request.BranchPhoneNumber).MaximumLength(20).MustBeIndianMobileWhenPresent();
        RuleFor(request => request)
            .Must(request => (request.Latitude is null) == (request.Longitude is null))
            .WithMessage("Enter both latitude and longitude, or leave both empty.");
    }
}

public sealed class UpdateAdminClinicBranchRequestValidator : AbstractValidator<UpdateAdminClinicBranchRequest>
{
    public UpdateAdminClinicBranchRequestValidator()
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
        RuleFor(request => request)
            .Must(request => (request.Latitude is null) == (request.Longitude is null))
            .WithMessage("Enter both latitude and longitude, or leave both empty.");
    }
}

public sealed class UpdateAdminClinicRequestValidator : AbstractValidator<UpdateAdminClinicRequest>
{
    public UpdateAdminClinicRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Type).NotEmpty().MaximumLength(40);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).MustBeIndianMobile();
        RuleFor(request => request.Email)
            .MaximumLength(160)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.Website).MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.RegistrationNumber).MaximumLength(40);
    }
}
