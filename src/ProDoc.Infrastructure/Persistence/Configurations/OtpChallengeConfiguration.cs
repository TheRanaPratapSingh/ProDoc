using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Identity;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges");
        builder.Property(challenge => challenge.PhoneE164).HasMaxLength(16).IsRequired();
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(challenge => challenge.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(challenge => new { challenge.PhoneE164, challenge.Purpose, challenge.CreatedAtUtc });
    }
}
