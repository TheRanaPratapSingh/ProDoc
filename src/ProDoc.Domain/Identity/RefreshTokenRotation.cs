namespace ProDoc.Domain.Identity;

public enum RefreshTokenDecision
{
    Rotate = 1,
    Reject = 2,
    RevokeFamily = 3
}

public static class RefreshTokenRotation
{
    public static RefreshTokenDecision Evaluate(RefreshToken? token, DateTimeOffset now)
    {
        if (token is null)
        {
            return RefreshTokenDecision.Reject;
        }

        if (token.WasRotatedOrRevoked)
        {
            return RefreshTokenDecision.RevokeFamily;
        }

        return token.IsActive(now)
            ? RefreshTokenDecision.Rotate
            : RefreshTokenDecision.Reject;
    }
}
