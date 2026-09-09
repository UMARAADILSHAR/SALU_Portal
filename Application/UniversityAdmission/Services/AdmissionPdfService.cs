using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SaluExamPortal.Application.UniversityAdmission.Models;

namespace SaluExamPortal.Application.UniversityAdmission.Services;

public interface IAdmissionPdfService
{
    byte[] GenerateApplicationFormPdf(AdmissionFormState form, string trackingId, string verificationUrl);
    byte[] GenerateAdmissionChallanPdf(AdmissionFormState form, string trackingId, decimal amount = 2500m);
}

public class AdmissionPdfService : IAdmissionPdfService
{
    private readonly IQrCodeService _qrService;

    public AdmissionPdfService(IQrCodeService qrService)
    {
        _qrService = qrService;
    }

    public byte[] GenerateApplicationFormPdf(AdmissionFormState form, string trackingId, string verificationUrl)
    {
        var qrCodeBytes = _qrService.GenerateQrCodePng(verificationUrl, 8);

        byte[]? photoBytes = null;
        if (!string.IsNullOrWhiteSpace(form.Personal.PhotoUrl) && form.Personal.PhotoUrl.StartsWith("data:image"))
        {
            try
            {
                var base64Data = form.Personal.PhotoUrl.Substring(form.Personal.PhotoUrl.IndexOf(',') + 1);
                photoBytes = Convert.FromBase64String(base64Data);
            }
            catch
            {
                photoBytes = null;
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, trackingId, qrCodeBytes, photoBytes));
                page.Content().Element(c => ComposeContent(c, form, trackingId));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateAdmissionChallanPdf(AdmissionFormState form, string trackingId, decimal amount = 2500m)
    {
        var qrCodeBytes = _qrService.GenerateQrCodePng($"SALU-CHALLAN:{trackingId}:PKR{amount}", 6);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                page.Content().Row(row =>
                {
                    row.RelativeItem().PaddingRight(10).Element(c => ComposeChallanCopy(c, "BANK COPY", form, trackingId, amount, qrCodeBytes));
                    row.RelativeItem().PaddingHorizontal(5).Element(c => ComposeChallanCopy(c, "UNIVERSITY COPY", form, trackingId, amount, qrCodeBytes));
                    row.RelativeItem().PaddingLeft(10).Element(c => ComposeChallanCopy(c, "STUDENT COPY", form, trackingId, amount, qrCodeBytes));
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, string trackingId, byte[] qrBytes, byte[]? photoBytes)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Left: QR Code & Tracking ID
                row.ConstantItem(85).Column(qrCol =>
                {
                    qrCol.Item().Width(75).Height(75).Image(qrBytes);
                    qrCol.Item().PaddingTop(2).Text(trackingId).FontSize(8).Bold().FontColor("#1b2a6b");
                });

                // Center: Official Title
                row.RelativeItem().PaddingHorizontal(10).Column(titleCol =>
                {
                    titleCol.Item().AlignCenter().Text("SHAH ABDUL LATIF UNIVERSITY, KHAIRPUR").FontSize(14).Bold().FontColor("#1b2a6b");
                    titleCol.Item().AlignCenter().Text("DIRECTORATE OF ADMISSIONS").FontSize(10).SemiBold().FontColor("#a87b00");
                    titleCol.Item().AlignCenter().Text("REGULAR ADMISSIONS APPLICATION FORM (2026-2027)").FontSize(10).Bold().FontColor("#1b2a6b");
                    titleCol.Item().AlignCenter().PaddingTop(4).Text($"Form Tracking ID: {trackingId}").FontSize(9).FontColor(Colors.Grey.Darken3);
                });

                // Right: Candidate Photo
                row.ConstantItem(85).Column(photoCol =>
                {
                    if (photoBytes != null && photoBytes.Length > 0)
                    {
                        photoCol.Item().Width(75).Height(95).Border(1).BorderColor(Colors.Grey.Lighten1).Image(photoBytes);
                    }
                    else
                    {
                        photoCol.Item().Width(75).Height(95).Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4)
                            .AlignCenter().AlignMiddle().Text("Photograph\n(Passport Size)").FontSize(7).FontColor(Colors.Grey.Darken1).AlignCenter();
                    }
                });
            });

            col.Item().PaddingTop(6).PaddingBottom(4).LineHorizontal(1.5f).LineColor("#1b2a6b");
        });
    }

    private void ComposeContent(IContainer container, AdmissionFormState form, string trackingId)
    {
        container.Column(col =>
        {
            // Section 1: Candidate Personal Information
            col.Item().PaddingTop(4).Element(c => SectionTitle(c, "1. PERSONAL INFORMATION"));
            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                });

                DataRow(table, "Full Name:", form.Personal.Name, "Father's Name:", form.Personal.FatherName);
                DataRow(table, "Surname / Caste:", form.Personal.Surname, "Guardian Name:", form.Personal.GuardianName);
                DataRow(table, "CNIC / B-Form:", form.Personal.Cnic, "Date of Birth:", form.Personal.DateOfBirth?.ToString("dd-MMM-yyyy") ?? "-");
                DataRow(table, "Gender:", form.Personal.Gender, "Religion:", form.Personal.Religion);
                DataRow(table, "Nationality:", form.Personal.Nationality, "Domicile District:", $"{form.Personal.DomicileDistrict} ({form.Personal.DomicileProvince})");
                DataRow(table, "Mobile No:", form.Personal.MobileNo, "Email Address:", form.Personal.Email);
                DataRow(table, "Current Address:", form.Personal.CurrentAddress, "Permanent Address:", form.Personal.PermanentAddress);
                DataRow(table, "Hostel Required:", form.Personal.AvailHostel ? "YES" : "NO", "Transport Required:", form.Personal.AvailTransport ? "YES" : "NO");
            });

            // Section 2: Applied Program & Quotas
            col.Item().PaddingTop(8).Element(c => SectionTitle(c, "2. PROGRAM APPLIED FOR & QUOTA DETAILS"));
            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                });

                DataRow(table, "Faculty:", form.Academics.Faculty, "Department:", form.Academics.Department);
                DataRow(table, "Program:", form.Academics.Program, "Qualifying Degree:", form.Academics.QualifyingDegree);
                DataRow(table, "Academic Year:", form.Academics.AcademicYear, "Shift / Timing:", "Morning / Regular");
                DataRow(table, "Self Finance:", form.Personal.SelfFinance ? "YES" : "NO", "Employed:", form.Personal.IsEmployed ? "YES" : "NO");
            });

            // Section 3: Academic Record
            col.Item().PaddingTop(8).Element(c => SectionTitle(c, "3. PREVIOUS ACADEMIC QUALIFICATIONS"));
            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Certificate / Degree").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Year").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Seat No").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Board / University").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Obt. Marks").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("Total").FontSize(7.5f).Bold().FontColor(Colors.White);
                    header.Cell().Background("#1b2a6b").Padding(3).Text("% / CGPA").FontSize(7.5f).Bold().FontColor(Colors.White);
                });

                if (form.Academics.History.Count == 0)
                {
                    table.Cell().ColumnSpan(7).Padding(4).AlignCenter().Text("No academic entries recorded").FontSize(8).FontColor(Colors.Grey.Medium);
                }
                else
                {
                    foreach (var q in form.Academics.History)
                    {
                        var pct = (double.TryParse(q.ObtainedMarks, out var obt) && double.TryParse(q.TotalMarks, out var tot) && tot > 0)
                            ? (obt / tot * 100).ToString("F1") + "%"
                            : "-";

                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.Examination).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.Year).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.SeatNo).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.Board).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.ObtainedMarks).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.TotalMarks).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(pct).FontSize(7.5f).Bold();
                    }
                }
            });

            // Section 4: Program Preferences
            if (form.Preferences.Count > 0)
            {
                col.Item().PaddingTop(8).Element(c => SectionTitle(c, "4. PROGRAM PREFERENCES"));
                col.Item().PaddingTop(3).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#334e8b").Padding(2.5f).Text("Choice").FontSize(7.5f).Bold().FontColor(Colors.White);
                        header.Cell().Background("#334e8b").Padding(2.5f).Text("Faculty").FontSize(7.5f).Bold().FontColor(Colors.White);
                        header.Cell().Background("#334e8b").Padding(2.5f).Text("Department").FontSize(7.5f).Bold().FontColor(Colors.White);
                        header.Cell().Background("#334e8b").Padding(2.5f).Text("Program").FontSize(7.5f).Bold().FontColor(Colors.White);
                    });

                    var idx = 1;
                    foreach (var pref in form.Preferences)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2.5f).Text($"#{idx++}").FontSize(7.5f).Bold();
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2.5f).Text(pref.Faculty).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2.5f).Text(pref.Department).FontSize(7.5f);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2.5f).Text(pref.Program).FontSize(7.5f);
                    }
                });
            }

            // Section 5: Legal Undertaking & Signatures
            col.Item().PaddingTop(8).Element(c => SectionTitle(c, "5. DECLARATION & UNDERTAKING"));
            col.Item().PaddingTop(3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(uCol =>
            {
                uCol.Item().Text("I hereby solemnly declare that all statements made in this admission application are true, complete and correct to the best of my knowledge. In the event of any information being found false, fabricated or incorrect at any stage, my admission shall stand cancelled ab-initio, and I shall be liable to disciplinary and legal action as per Shah Abdul Latif University regulations.")
                    .FontSize(7.2f).Italic().FontColor(Colors.Grey.Darken3);
            });

            col.Item().PaddingTop(18).Row(sigRow =>
            {
                sigRow.RelativeItem().Column(sc =>
                {
                    sc.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    sc.Item().PaddingTop(3).AlignCenter().Text("Candidate Signature").FontSize(8).Bold();
                });
                sigRow.ConstantItem(40);
                sigRow.RelativeItem().Column(sc =>
                {
                    sc.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    sc.Item().PaddingTop(3).AlignCenter().Text("Father / Guardian Signature").FontSize(8).Bold();
                });
                sigRow.ConstantItem(40);
                sigRow.RelativeItem().Column(sc =>
                {
                    sc.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    sc.Item().PaddingTop(3).AlignCenter().Text("Admission Scrutiny Officer").FontSize(8).Bold();
                });
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"Generated on: {DateTime.Now:dd-MMM-yyyy hh:mm tt} (PKT) | Shah Abdul Latif University Khairpur Official Admission Portal")
                    .FontSize(6.5f).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void SectionTitle(IContainer container, string title)
    {
        container.Background("#f0f4f9").BorderLeft(3).BorderColor("#1b2a6b").PaddingVertical(3).PaddingHorizontal(6)
            .Text(title).FontSize(8.5f).Bold().FontColor("#1b2a6b");
    }

    private static void DataRow(TableDescriptor table, string label1, string val1, string label2, string val2)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(label1).FontSize(7.5f).SemiBold().FontColor("#334e8b");
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(string.IsNullOrWhiteSpace(val1) ? "-" : val1).FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(label2).FontSize(7.5f).SemiBold().FontColor("#334e8b");
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2.5f).Text(string.IsNullOrWhiteSpace(val2) ? "-" : val2).FontSize(7.5f);
    }

    private void ComposeChallanCopy(IContainer container, string copyName, AdmissionFormState form, string trackingId, decimal amount, byte[] qrBytes)
    {
        container.Border(1).BorderColor("#1b2a6b").Padding(8).Column(col =>
        {
            col.Item().AlignCenter().Text("HBL / NBP / Sindh Bank").FontSize(8).Bold().FontColor("#1b2a6b");
            col.Item().AlignCenter().Text("SHAH ABDUL LATIF UNIVERSITY").FontSize(8).Bold();
            col.Item().AlignCenter().Text("Admission Processing Fee Challan").FontSize(7.5f);
            col.Item().AlignCenter().PaddingVertical(2).Background("#1b2a6b").PaddingHorizontal(6).Text(copyName).FontSize(7.5f).Bold().FontColor(Colors.White);

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Challan No: {trackingId}").FontSize(7.5f).Bold();
                    c.Item().Text($"A/C No: 00427900161103 (HBL)").FontSize(7);
                    c.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}").FontSize(7);
                });
                row.ConstantItem(45).Image(qrBytes);
            });

            col.Item().PaddingTop(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            col.Item().PaddingTop(3).Column(c =>
            {
                c.Item().Text($"Candidate: {form.Personal.Name}").FontSize(7.5f).Bold();
                c.Item().Text($"Father: {form.Personal.FatherName}").FontSize(7);
                c.Item().Text($"CNIC: {form.Personal.Cnic}").FontSize(7);
                c.Item().Text($"Program: {form.Academics.Program}").FontSize(7);
                c.Item().Text($"Fee Amount: PKR {amount:N0}/-").FontSize(8).Bold().FontColor("#a87b00");
                c.Item().Text($"(Two Thousand Five Hundred Only)").FontSize(6.5f).Italic();
            });

            col.Item().PaddingTop(12).Row(r =>
            {
                r.RelativeItem().Column(sc =>
                {
                    sc.Item().LineHorizontal(0.5f);
                    sc.Item().PaddingTop(2).AlignCenter().Text("Candidate Sign").FontSize(6.5f);
                });
                r.ConstantItem(15);
                r.RelativeItem().Column(sc =>
                {
                    sc.Item().LineHorizontal(0.5f);
                    sc.Item().PaddingTop(2).AlignCenter().Text("Bank Cashier").FontSize(6.5f);
                });
            });
        });
    }
}
