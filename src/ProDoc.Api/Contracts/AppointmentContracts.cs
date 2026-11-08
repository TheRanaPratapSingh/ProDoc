using FluentValidation;
using ProDoc.Api.Validation;

namespace ProDoc.Api.Contracts;

public sealed class HoldAppointmentRequest
{
    public Guid ScheduleId { get; set; }

    public DateOnly Date { get; set; }

    public string Start { get; set; } = string.Empty;
}

public sealed class HoldAppointmentRequestValidator : AbstractValidator<HoldAppointmentRequest>
{
    public HoldAppointmentRequestValidator()
    {
        RuleFor(request => request.ScheduleId).NotEmpty();
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Start).Must(BeTime).WithMessage("Enter a start time.");
    }

    internal static bool BeTime(string value) => TimeOnly.TryParse(value, out _);
}

public sealed class ConfirmAppointmentRequest
{
    public Guid? FamilyMemberId { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional doctor follow-up recommendation to link after booking.</summary>
    public Guid? FollowUpId { get; set; }
}

public sealed class ConfirmAppointmentRequestValidator : AbstractValidator<ConfirmAppointmentRequest>
{
    public ConfirmAppointmentRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(120);
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}

public sealed class AppointmentNoteRequest
{
    public string? Reason { get; set; }
}

public sealed class AppointmentNoteRequestValidator : AbstractValidator<AppointmentNoteRequest>
{
    public AppointmentNoteRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(120);
    }
}

public sealed class RescheduleAppointmentRequest
{
    public DateOnly Date { get; set; }

    public string Start { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Start).Must(HoldAppointmentRequestValidator.BeTime).WithMessage("Enter a start time.");
        RuleFor(request => request.Reason).MaximumLength(120);
    }
}

public sealed class AppointmentDecisionRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class AppointmentDecisionRequestValidator : AbstractValidator<AppointmentDecisionRequest>
{
    public AppointmentDecisionRequestValidator()
    {
        RuleFor(request => request.Decision).Must(value => value is "Approve" or "Reject");
        RuleFor(request => request.Reason).MaximumLength(120);
    }
}

public sealed class WalkInAppointmentRequest
{
    public Guid ScheduleId { get; set; }

    public DateOnly Date { get; set; }

    public string Start { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;
}

public sealed class WalkInAppointmentRequestValidator : AbstractValidator<WalkInAppointmentRequest>
{
    public WalkInAppointmentRequestValidator()
    {
        RuleFor(request => request.ScheduleId).NotEmpty();
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Start).Must(HoldAppointmentRequestValidator.BeTime).WithMessage("Enter a start time.");
        RuleFor(request => request.PatientName).MinimumLength(2).MaximumLength(80);
    }
}

public sealed class DeskBookAppointmentRequest
{
    public Guid ScheduleId { get; set; }

    public DateOnly Date { get; set; }

    public string Start { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }
}

public sealed class DeskBookAppointmentRequestValidator : AbstractValidator<DeskBookAppointmentRequest>
{
    public DeskBookAppointmentRequestValidator()
    {
        RuleFor(request => request.ScheduleId).NotEmpty();
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.Start).Must(HoldAppointmentRequestValidator.BeTime).WithMessage("Enter a start time.");
        RuleFor(request => request.PatientName).MinimumLength(2).MaximumLength(80);
        RuleFor(request => request.Phone).MaximumLength(20);
        RuleFor(request => request.Reason).MaximumLength(120);
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}

public sealed class DeskRegisterPatientRequest
{
    public Guid ClinicId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}

public sealed class DeskRegisterPatientRequestValidator : AbstractValidator<DeskRegisterPatientRequest>
{
    public DeskRegisterPatientRequestValidator()
    {
        RuleFor(request => request.ClinicId).NotEmpty();
        RuleFor(request => request.FullName).MinimumLength(2).MaximumLength(80);
        RuleFor(request => request.Phone).NotEmpty().MaximumLength(20).MustBeIndianMobile();
    }
}

public sealed class FamilyMemberRequest
{
    public string FullName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string Relationship { get; set; } = string.Empty;
}

public sealed class FamilyMemberRequestValidator : AbstractValidator<FamilyMemberRequest>
{
    public FamilyMemberRequestValidator()
    {
        RuleFor(request => request.FullName).MinimumLength(2).MaximumLength(80);
        RuleFor(request => request.Relationship).Must(value => value is "spouse" or "child" or "parent" or "other");
        RuleFor(request => request.Gender).MaximumLength(32);
    }
}

public sealed class CreateAppointmentShareLinkRequest
{
    /// <summary>Optional lifetime in hours (1–720). Defaults to configured sharing lifetime.</summary>
    public int? LifetimeHours { get; set; }
}

public sealed class CreateAppointmentShareLinkRequestValidator : AbstractValidator<CreateAppointmentShareLinkRequest>
{
    public CreateAppointmentShareLinkRequestValidator()
    {
        RuleFor(request => request.LifetimeHours)
            .InclusiveBetween(1, 720)
            .When(request => request.LifetimeHours is not null);
    }
}
