using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("09876543210")]
    [InlineData("+91 98765 43210")]
    [InlineData("919876543210")]
    public void Indian_mobile_numbers_normalize_to_e164(string input)
    {
        var phone = PhoneNumber.ParseIndianMobile(input);

        Assert.Equal("+919876543210", phone.E164);
        Assert.Equal("+91******3210", phone.Masked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("5876543210")]
    [InlineData("12345")]
    [InlineData("987654321")] // 9 digits
    [InlineData("98765432101")] // 11 digits without trunk/country prefix
    [InlineData("9198765432101")] // 13 digits / oversized
    [InlineData("+1 202 555 0143")]
    public void Invalid_numbers_are_rejected(string input)
    {
        var exception = Assert.Throws<DomainException>(() => PhoneNumber.ParseIndianMobile(input));

        Assert.Equal("phone_invalid", exception.Code);
        Assert.Equal(400, exception.StatusCode);
        Assert.False(PhoneNumber.TryParseIndianMobile(input, out _));
    }

    [Fact]
    public void TryParse_returns_false_for_null_or_whitespace()
    {
        Assert.False(PhoneNumber.TryParseIndianMobile(null, out _));
        Assert.False(PhoneNumber.TryParseIndianMobile("   ", out _));
    }
}
