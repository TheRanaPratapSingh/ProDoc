using ProDoc.Domain.Common;
using ProDoc.Domain.Patients;

namespace ProDoc.Domain.Tests;

public class PatientTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Profile_accepts_english_and_hindi_names()
    {
        var patient = Patient.Register(Guid.NewGuid(), Now);

        patient.UpdateProfile("  Asha   Rao ", Languages.Hindi, Now.AddMinutes(1));

        Assert.Equal("Asha Rao", patient.FullName);
        Assert.Equal(Languages.Hindi, patient.PreferredLanguage);

        patient.UpdateProfile("आशा शर्मा", Languages.English, Now.AddMinutes(2));
        Assert.Equal("आशा शर्मा", patient.FullName);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Asha@Rao")]
    [InlineData("1234")]
    public void Invalid_names_are_rejected(string name)
    {
        var patient = Patient.Register(Guid.NewGuid(), Now);

        var exception = Assert.Throws<DomainException>(() => patient.UpdateProfile(name, Languages.English, Now));

        Assert.Equal("name_invalid", exception.Code);
    }

    [Fact]
    public void Deletion_request_is_idempotent_and_blocks_profile_changes()
    {
        var patient = Patient.Register(Guid.NewGuid(), Now);
        patient.RequestDeletion(Now.AddMinutes(5));
        patient.RequestDeletion(Now.AddMinutes(10));

        Assert.Equal(Now.AddMinutes(5), patient.DeletionRequestedAtUtc);
        Assert.Throws<DomainException>(() => patient.UpdateProfile("Asha Rao", Languages.English, Now.AddMinutes(11)));
    }
}
