using ProDoc.Application.Clinical;
using ProDoc.Domain.Clinical;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProDoc.Infrastructure.Clinical;

public sealed class PrescriptionPdfGenerator : IPrescriptionPdfGenerator
{
    static PrescriptionPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateAsync(Prescription prescription, PrescriptionVersion version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor(Colors.Grey.Darken3));
                page.Header().Column(column =>
                {
                    column.Item().Text("ProDoc").FontSize(22).SemiBold().FontColor(Color.FromHex("#0F766E"));
                    column.Item().Text("Digital prescription").FontSize(11).FontColor(Colors.Grey.Darken1);
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Color.FromHex("#0F766E"));
                });
                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text(text =>
                    {
                        text.Span("Prescription ").SemiBold();
                        text.Span(prescription.Number);
                        text.Span($"  ·  v{version.VersionNumber}");
                    });
                    column.Item().Text($"Consultation date (UTC): {version.ConsultationDateUtc:yyyy-MM-dd HH:mm}");
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Patient").SemiBold().FontColor(Color.FromHex("#1D4ED8"));
                            left.Item().Text(version.PatientNameSnapshot);
                        });
                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Text("Doctor").SemiBold().FontColor(Color.FromHex("#1D4ED8"));
                            right.Item().Text(version.DoctorNameSnapshot);
                            if (!string.IsNullOrWhiteSpace(version.DoctorRegistrationSnapshot))
                            {
                                right.Item().Text(version.DoctorRegistrationSnapshot);
                            }
                        });
                    });
                    column.Item().Column(clinic =>
                    {
                        clinic.Item().Text("Clinic").SemiBold().FontColor(Color.FromHex("#1D4ED8"));
                        clinic.Item().Text($"{version.ClinicNameSnapshot} — {version.BranchNameSnapshot}");
                        if (!string.IsNullOrWhiteSpace(version.BranchAddressSnapshot))
                        {
                            clinic.Item().Text(version.BranchAddressSnapshot);
                        }
                    });
                    if (!string.IsNullOrWhiteSpace(version.Diagnosis))
                    {
                        column.Item().Text("Diagnosis").SemiBold();
                        column.Item().Text(version.Diagnosis);
                    }

                    if (!string.IsNullOrWhiteSpace(version.Symptoms))
                    {
                        column.Item().Text("Symptoms").SemiBold();
                        column.Item().Text(version.Symptoms);
                    }

                    if (!string.IsNullOrWhiteSpace(version.ClinicalObservations))
                    {
                        column.Item().Text("Clinical observations").SemiBold();
                        column.Item().Text(version.ClinicalObservations);
                    }

                    column.Item().PaddingTop(6).Text("Medicines").SemiBold().FontSize(12);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Medicine");
                            header.Cell().Element(HeaderCell).Text("Dosage");
                            header.Cell().Element(HeaderCell).Text("Frequency");
                            header.Cell().Element(HeaderCell).Text("Duration");
                            header.Cell().Element(HeaderCell).Text("Route");
                        });
                        foreach (var item in version.Items.OrderBy(medicine => medicine.SortOrder))
                        {
                            table.Cell().Element(BodyCell).Text(item.MedicineName);
                            table.Cell().Element(BodyCell).Text(item.Dosage);
                            table.Cell().Element(BodyCell).Text(item.Frequency);
                            table.Cell().Element(BodyCell).Text(item.Duration);
                            table.Cell().Element(BodyCell).Text(item.Route);
                        }
                    });

                    foreach (var item in version.Items.Where(medicine => !string.IsNullOrWhiteSpace(medicine.Instructions)))
                    {
                        column.Item().Text($"{item.MedicineName}: {item.Instructions}");
                    }

                    if (!string.IsNullOrWhiteSpace(version.Advice))
                    {
                        column.Item().Text("Advice").SemiBold();
                        column.Item().Text(version.Advice);
                    }

                    if (version.FollowUpDate is not null)
                    {
                        column.Item().Text($"Follow-up date: {version.FollowUpDate:yyyy-MM-dd}");
                    }

                    if (!string.IsNullOrWhiteSpace(version.AmendmentReason))
                    {
                        column.Item().Text($"Amendment reason: {version.AmendmentReason}");
                    }

                    column.Item().PaddingTop(20).Text("Doctor attestation").SemiBold();
                    column.Item().Text(
                        "Finalized in ProDoc by the treating doctor. This document is not a cryptographically signed e-prescription under Indian IT Rules unless a verified digital signature mechanism is separately configured.");
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by ProDoc · Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

    private static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
}
