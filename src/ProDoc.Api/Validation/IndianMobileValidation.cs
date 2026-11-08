using FluentValidation;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Validation;

public static class IndianMobileValidation
{
    public const string InvalidMessage = "Enter a valid Indian mobile number.";

    public static bool IsValidIndianMobile(string? input) =>
        PhoneNumber.TryParseIndianMobile(input, out _);

    public static bool IsValidIndianMobileOrEmpty(string? input) =>
        string.IsNullOrWhiteSpace(input) || IsValidIndianMobile(input);

    public static IRuleBuilderOptions<T, string?> MustBeIndianMobile<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(IsValidIndianMobile).WithMessage(InvalidMessage);

    public static IRuleBuilderOptions<T, string?> MustBeIndianMobileWhenPresent<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(IsValidIndianMobileOrEmpty).WithMessage(InvalidMessage);
}
