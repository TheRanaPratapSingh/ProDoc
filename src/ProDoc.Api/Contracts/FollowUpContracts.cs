using FluentValidation;

namespace ProDoc.Api.Contracts;

public sealed class CreateFollowUpRequest
{
    public Guid? SourceAppointmentId { get; set; }

    public Guid? SourceConsultationId { get; set; }

    public Guid? PatientUserId { get; set; }

    public Guid? FamilyMemberId { get; set; }

    public string? PatientName { get; set; }

    public Guid? ClinicId { get; set; }

    public Guid? BranchId { get; set; }

    public DateOnly RecommendedDate { get; set; }

    public string? FollowUpNote { get; set; }

    public bool RequiresBooking { get; set; } = true;
}

public sealed class CreateFollowUpRequestValidator : AbstractValidator<CreateFollowUpRequest>
{
    public CreateFollowUpRequestValidator()
    {
        RuleFor(item => item.RecommendedDate).NotEmpty();
        RuleFor(item => item.FollowUpNote).MaximumLength(500).When(item => item.FollowUpNote is not null);
        RuleFor(item => item)
            .Must(item => item.SourceConsultationId is not null
                || item.SourceAppointmentId is not null
                || (item.PatientUserId is not null && item.ClinicId is not null && item.BranchId is not null))
            .WithMessage("Provide a consultation, appointment, or patient with clinic and branch.");
    }
}

public sealed class UpdateFollowUpRequest
{
    public DateOnly RecommendedDate { get; set; }

    public string? FollowUpNote { get; set; }

    public bool? RequiresBooking { get; set; }
}

public sealed class UpdateFollowUpRequestValidator : AbstractValidator<UpdateFollowUpRequest>
{
    public UpdateFollowUpRequestValidator()
    {
        RuleFor(item => item.RecommendedDate).NotEmpty();
        RuleFor(item => item.FollowUpNote).MaximumLength(500).When(item => item.FollowUpNote is not null);
    }
}

public sealed class LinkFollowUpAppointmentRequest
{
    public Guid AppointmentId { get; set; }
}

public sealed class LinkFollowUpAppointmentRequestValidator : AbstractValidator<LinkFollowUpAppointmentRequest>
{
    public LinkFollowUpAppointmentRequestValidator()
    {
        RuleFor(item => item.AppointmentId).NotEmpty();
    }
}
