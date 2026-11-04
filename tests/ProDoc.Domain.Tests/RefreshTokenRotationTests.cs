using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Tests;

public class RefreshTokenRotationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_token_is_rejected()
    {
        Assert.Equal(RefreshTokenDecision.Reject, RefreshTokenRotation.Evaluate(null, Now));
    }

    [Fact]
    public void Active_token_can_rotate()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(30), "Pixel");

        Assert.Equal(RefreshTokenDecision.Rotate, RefreshTokenRotation.Evaluate(token, Now.AddMinutes(1)));
    }

    [Fact]
    public void Expired_token_is_rejected_without_revoking_the_family()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddMinutes(5), null);

        Assert.Equal(RefreshTokenDecision.Reject, RefreshTokenRotation.Evaluate(token, Now.AddMinutes(6)));
    }

    [Fact]
    public void Reuse_of_a_rotated_token_revokes_the_family()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(30), null);
        token.Revoke(Now.AddMinutes(1), Guid.NewGuid());

        Assert.Equal(RefreshTokenDecision.RevokeFamily, RefreshTokenRotation.Evaluate(token, Now.AddMinutes(2)));
    }

    [Fact]
    public void Device_name_over_80_characters_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(1), new string('a', 81)));
    }
}
