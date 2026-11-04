using ProDoc.Domain.Common;

namespace ProDoc.Domain.Identity;

public readonly record struct PhoneNumber
{
    private PhoneNumber(string e164) => E164 = e164;

    public string E164 { get; }

    public string Masked => string.Concat("+91******", E164.AsSpan(E164.Length - 4));

    public static PhoneNumber ParseIndianMobile(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw DomainException.Validation("phone_invalid", "Enter a valid Indian mobile number.");
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        if (digits.Length != 10 || digits[0] is < '6' or > '9')
        {
            throw DomainException.Validation("phone_invalid", "Enter a valid Indian mobile number.");
        }

        return new PhoneNumber("+91" + digits);
    }
}
