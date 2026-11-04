using ProDoc.Domain.Clinical;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Tests;

public sealed class PrescriptionTests
{
    [Fact]
    public void Finalized_prescription_cannot_be_edited_without_amendment()
    {
        var now = DateTimeOffset.UtcNow;
        var prescription = Prescription.CreateDraft(
            "RX202601010001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "Patient",
            "Dr. Test",
            "REG-1",
            "Clinic",
            "Branch",
            "City",
            now,
            now,
            Guid.NewGuid(),
            Guid.NewGuid());
        prescription.UpdateDraft(
            "Viral fever",
            "Fever",
            null,
            "Rest",
            null,
            [(null, "Paracetamol", "500mg", "TDS", "3 days", "Oral", null)],
            now);
        prescription.Finalize(Guid.NewGuid(), now, "abc.pdf");

        var edit = Assert.Throws<DomainException>(() =>
            prescription.UpdateDraft("Changed", null, null, null, null, [(null, "Ibuprofen", "400mg", "BD", "2 days", "Oral", null)], now));
        Assert.Equal("prescription_immutable", edit.Code);

        var draft = prescription.BeginAmendment(Guid.NewGuid(), "Dose correction", now.AddMinutes(1));
        Assert.Equal(PrescriptionStatus.Draft, prescription.Status);
        Assert.Equal(2, draft.VersionNumber);
        prescription.UpdateDraft(
            "Viral fever",
            "Fever",
            null,
            "Rest",
            null,
            [(null, "Paracetamol", "650mg", "TDS", "3 days", "Oral", "After food")],
            now.AddMinutes(2));
        prescription.Finalize(Guid.NewGuid(), now.AddMinutes(3), "def.pdf");
        Assert.Equal(PrescriptionStatus.Amended, prescription.Status);
        Assert.Equal(2, prescription.Versions.Count);
        Assert.Contains(prescription.Versions, version => version.Status == PrescriptionStatus.Finalized);
    }
}
