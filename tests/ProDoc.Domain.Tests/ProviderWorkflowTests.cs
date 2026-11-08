using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Domain.Tests;

public sealed class ProviderWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Cardiology = Guid.Parse("b1d0c2a0-5c1e-4a11-9c1a-000000000002");

    [Fact]
    public void Submit_requires_a_complete_application_and_does_not_approve_it()
    {
        var doctor = Doctor.Begin(Guid.NewGuid(), "en", Now);
        var incomplete = Assert.Throws<DomainException>(() => doctor.Submit(Now));
        Assert.Equal("application_incomplete", incomplete.Code);

        Complete(doctor);
        doctor.Submit(Now);

        Assert.Equal(VerificationStatus.Submitted, doctor.Status);
        Assert.StartsWith("DOC-2026-", doctor.Reference);
        Assert.False(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));
        var duplicate = Assert.Throws<DomainException>(() => doctor.Submit(Now));
        Assert.Equal("application_already_submitted", duplicate.Code);
    }

    [Fact]
    public void Approved_profile_edits_stay_approved_until_a_regulated_credential_changes()
    {
        var doctor = SubmittedDoctor();
        doctor.Review(ReviewDecision.Approve, null, Now);
        Assert.True(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));

        doctor.UpdateProfessional("Dr. Asha Rao", "Updated biography", 12, ["en", "hi"], ["Heart failure"], Now.AddMinutes(1));
        Assert.Equal(VerificationStatus.Approved, doctor.Status);
        Assert.True(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));

        doctor.UpdateRegistration("MCI-200", "National Medical Commission", "Delhi", "permanent", new DateOnly(2014, 5, 1), null, Now.AddMinutes(2));
        Assert.Equal(VerificationStatus.Submitted, doctor.Status);
        Assert.False(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));
        Assert.Equal(2, doctor.Registrations.Count(registration => registration.Version > 0));
        Assert.Single(doctor.Registrations, registration => registration.IsCurrent);
    }

    [Fact]
    public void Suspended_and_expired_doctors_are_not_bookable()
    {
        var doctor = SubmittedDoctor();
        doctor.Review(ReviewDecision.Approve, null, Now);
        doctor.Review(ReviewDecision.Suspend, "Licence check failed", Now.AddMinutes(1));
        Assert.Equal(VerificationStatus.Suspended, doctor.Status);
        Assert.False(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));

        var expired = SubmittedDoctor();
        expired.Review(ReviewDecision.Approve, null, Now);
        Assert.True(expired.RefreshExpiry(new DateOnly(2031, 1, 1), Now.AddYears(5)));
        Assert.Equal(VerificationStatus.Expired, expired.Status);
        Assert.False(expired.IsPubliclyBookable(new DateOnly(2031, 1, 1)));
    }

    [Fact]
    public void A_doctor_can_practice_independently_or_at_more_than_one_clinic()
    {
        var doctor = SubmittedDoctor();
        Assert.Equal(VerificationStatus.Submitted, doctor.Status);

        var first = DoctorClinicAffiliation.Create(doctor.Id, Guid.NewGuid(), null, AssociationKind.Independent, true, Now);
        var second = DoctorClinicAffiliation.Create(doctor.Id, Guid.NewGuid(), Guid.NewGuid(), AssociationKind.Visiting, false, Now);
        Assert.NotEqual(first.ClinicId, second.ClinicId);
        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Invite_provisioning_allows_a_single_display_name_without_last_name()
    {
        var doctor = Doctor.Begin(Guid.NewGuid(), "en", Now);
        doctor.UpdateBasics("Akash", null, null, null, null, "en", Now);
        doctor.UpdateProfessional("Akash", null, 5, ["en"], null, Now);
        doctor.SetSpecializations([Cardiology], Cardiology, Now);
        doctor.UpdateRegistration(
            "MCI-AKASH",
            "National Medical Commission",
            "India",
            "permanent",
            new DateOnly(2018, 1, 1),
            null,
            Now);

        doctor.ProvisionApproved(Now);

        Assert.Equal(VerificationStatus.Approved, doctor.Status);
        Assert.Equal("Akash", doctor.DisplayName);
        Assert.True(doctor.IsPubliclyBookable(DateOnly.FromDateTime(Now.UtcDateTime)));
    }

    [Fact]
    public void Clinic_submission_requires_a_branch_and_registration_unless_it_is_an_individual_practice()
    {
        var clinic = Clinic.Begin(Guid.NewGuid(), Now);
        clinic.UpdateDetails("Rao Clinic", ClinicType.Clinic, "Rao Clinic LLP", null, "9876543210", null, null, 2018, null, null, null, null, Now);
        var missingRegistration = Assert.Throws<DomainException>(() => clinic.Submit(Now));
        Assert.Equal("application_incomplete", missingRegistration.Code);

        clinic.UpdateDetails(
            "Rao Clinic",
            ClinicType.IndividualPractice,
            null,
            null,
            "9876543210",
            null,
            null,
            2018,
            null,
            null,
            null,
            null,
            Now);
        var missingBranch = Assert.Throws<DomainException>(() => clinic.Submit(Now));
        Assert.Equal("application_incomplete", missingBranch.Code);

        clinic.AddBranch("Main", "12 Lake Road", null, "Pune", "Maharashtra", "411001", null, null, null, true, Now);
        clinic.AddBranch("Camp", "4 MG Road", null, "Pune", "Maharashtra", "411001", 18.52m, 73.85m, null, false, Now);
        clinic.Submit(Now);

        Assert.Equal(VerificationStatus.Submitted, clinic.Status);
        Assert.Equal(2, clinic.Branches.Count);
        Assert.False(clinic.IsPubliclyListed(DateOnly.FromDateTime(Now.UtcDateTime)));
        clinic.Review(ReviewDecision.Approve, null, Now);
        Assert.True(clinic.IsPubliclyListed(DateOnly.FromDateTime(Now.UtcDateTime)));

        clinic.UpdateDetails(
            "Rao Clinic",
            ClinicType.IndividualPractice,
            null,
            null,
            "9876543210",
            null,
            null,
            2018,
            "REG-9",
            "Local authority",
            "Maharashtra",
            null,
            Now.AddMinutes(1));
        Assert.Equal(VerificationStatus.Submitted, clinic.Status);
        Assert.False(clinic.IsPubliclyListed(DateOnly.FromDateTime(Now.UtcDateTime)));
    }

    private static Doctor SubmittedDoctor()
    {
        var doctor = Doctor.Begin(Guid.NewGuid(), "hi", Now);
        Complete(doctor);
        doctor.Submit(Now);
        return doctor;
    }

    private static void Complete(Doctor doctor)
    {
        doctor.UpdateBasics("Asha", "Rao", new DateOnly(1990, 1, 15), "female", "asha@example.com", "en", Now);
        doctor.UpdateProfessional("Dr. Asha Rao", "Cardiologist", 11, ["en", "hi"], ["Heart failure"], Now);
        doctor.AddQualification("MBBS", "Grant Medical College", 2012, Now);
        doctor.SetSpecializations([Cardiology], Cardiology, Now);
        doctor.UpdateRegistration("MCI-100", "National Medical Commission", "Maharashtra", "permanent", new DateOnly(2014, 5, 1), new DateOnly(2030, 5, 1), Now);
        doctor.AttachDocument(DocumentCategory.ProfilePhoto, "photo.png", "image/png", 12, "photo", "signature-allow-list", "checked", Now);
        doctor.AttachDocument(DocumentCategory.RegistrationCertificate, "registration.pdf", "application/pdf", 12, "certificate", "signature-allow-list", "checked", Now);
    }
}
