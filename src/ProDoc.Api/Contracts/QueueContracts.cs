using FluentValidation;
using ProDoc.Api.Validation;

namespace ProDoc.Api.Contracts;

public sealed class AssignStaffRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Role { get; set; } = "Receptionist";

    public Guid? BranchId { get; set; }
}

public sealed class AssignStaffRequestValidator : AbstractValidator<AssignStaffRequest>
{
    public AssignStaffRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).MustBeIndianMobile();
        RuleFor(request => request.Role).Must(value => value is "Receptionist" or "ClinicStaff");
    }
}

public sealed class UpdateQueueSettingsRequest
{
    public string TokenPrefix { get; set; } = "A";

    public bool ScheduledPatientPriority { get; set; } = true;

    public int EarlyCheckInMinutes { get; set; } = 60;

    public int LateCheckInMinutes { get; set; } = 120;

    public bool AllowWalkIns { get; set; } = true;

    public bool AllowPriorityPatients { get; set; }
}

public sealed class UpdateQueueSettingsRequestValidator : AbstractValidator<UpdateQueueSettingsRequest>
{
    public UpdateQueueSettingsRequestValidator()
    {
        RuleFor(request => request.TokenPrefix).MinimumLength(1).MaximumLength(4);
        RuleFor(request => request.EarlyCheckInMinutes).InclusiveBetween(0, 10_080);
        RuleFor(request => request.LateCheckInMinutes).InclusiveBetween(0, 10_080);
    }
}

public sealed class QrCheckInRequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class QrCheckInRequestValidator : AbstractValidator<QrCheckInRequest>
{
    public QrCheckInRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(128);
    }
}

public sealed class DeskWalkInRequest
{
    public Guid DoctorId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public string? Reason { get; set; }

    public decimal? ConsultationFee { get; set; }
}

public sealed class DeskWalkInRequestValidator : AbstractValidator<DeskWalkInRequest>
{
    public DeskWalkInRequestValidator()
    {
        RuleFor(request => request.DoctorId).NotEmpty();
        RuleFor(request => request.PatientName).MinimumLength(2).MaximumLength(80);
        RuleFor(request => request.ContactNumber).MaximumLength(20).MustBeIndianMobileWhenPresent();
        RuleFor(request => request.Reason).MaximumLength(120);
        RuleFor(request => request.ConsultationFee).InclusiveBetween(0, 100_000);
    }
}

public sealed class QueueNoteRequest
{
    public string? Reason { get; set; }
}

public sealed class QueueNoteRequestValidator : AbstractValidator<QueueNoteRequest>
{
    public QueueNoteRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(120);
    }
}
