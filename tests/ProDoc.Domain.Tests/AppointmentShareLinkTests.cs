using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Tests;

public sealed class AppointmentShareLinkTests
{
    [Fact]
    public void Create_stores_hash_and_expiry()
    {
        var raw = AppointmentShareLink.CreateRawToken();
        var now = DateTimeOffset.UtcNow;
        var link = AppointmentShareLink.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppointmentShareLink.HashToken(raw),
            now,
            TimeSpan.FromDays(2));

        Assert.True(link.IsActive(now));
        Assert.Equal(AppointmentShareLink.HashToken(raw), link.TokenHash);
        Assert.Equal(now.AddDays(2), link.ExpiresAtUtc);
        Assert.Null(link.RevokedAtUtc);
    }

    [Fact]
    public void Revoked_or_expired_link_is_inactive()
    {
        var raw = AppointmentShareLink.CreateRawToken();
        var now = DateTimeOffset.UtcNow;
        var link = AppointmentShareLink.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppointmentShareLink.HashToken(raw),
            now,
            TimeSpan.FromHours(1));

        link.Revoke(now);
        Assert.False(link.IsActive(now));

        var expired = AppointmentShareLink.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppointmentShareLink.HashToken(AppointmentShareLink.CreateRawToken()),
            now.AddHours(-3),
            TimeSpan.FromHours(1));
        Assert.False(expired.IsActive(now));
    }

    [Fact]
    public void RecordAccess_rejects_revoked_link()
    {
        var raw = AppointmentShareLink.CreateRawToken();
        var now = DateTimeOffset.UtcNow;
        var link = AppointmentShareLink.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppointmentShareLink.HashToken(raw),
            now);

        link.Revoke(now);
        var error = Assert.Throws<DomainException>(() => link.RecordAccess(now));
        Assert.Equal("share_link_unavailable", error.Code);
    }

    [Fact]
    public void HashToken_is_stable_and_case_insensitive_input_trimmed()
    {
        var raw = AppointmentShareLink.CreateRawToken();
        Assert.Equal(AppointmentShareLink.HashToken(raw), AppointmentShareLink.HashToken($"  {raw}  "));
    }
}
