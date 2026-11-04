using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Tests;

public class OtpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_challenge_can_be_consumed_once()
    {
        var challenge = OtpChallenge.Issue("+919876543210", "hash", OtpPurpose.SignIn, Now, TimeSpan.FromMinutes(5), 5);

        Assert.True(challenge.IsActive(Now.AddMinutes(4)));
        challenge.Consume(Now.AddMinutes(1));

        Assert.False(challenge.IsActive(Now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => challenge.Consume(Now.AddMinutes(2)));
    }

    [Fact]
    public void Attempts_above_the_limit_deactivate_the_challenge()
    {
        var challenge = OtpChallenge.Issue("+919876543210", "hash", OtpPurpose.SignIn, Now, TimeSpan.FromMinutes(5), 2);

        challenge.RegisterFailedAttempt();
        Assert.True(challenge.IsActive(Now));
        challenge.RegisterFailedAttempt();

        Assert.False(challenge.IsActive(Now));
    }

    [Fact]
    public void Expired_challenge_is_inactive()
    {
        var challenge = OtpChallenge.Issue("+919876543210", "hash", OtpPurpose.SignIn, Now, TimeSpan.FromMinutes(5), 5);

        Assert.False(challenge.IsActive(Now.AddMinutes(5)));
    }
}
