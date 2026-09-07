using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SaluExamPortal.Application.Services;

public sealed record DocumentAuthenticityResult(
    bool IsAuthentic,
    string Status, // "Verified", "Failed", "Suspect", "PendingReview"
    int AuthenticityScore, // 0 - 100
    string DocumentType,
    string? Reason,
    string? DetectedCnic,
    string? DetectedName,
    string? DetectedFatherName,
    string? DetectedBoard,
    string? DetectedSeatNo,
    int? DetectedPassingYear,
    int? DetectedObtainedMarks,
    int? DetectedTotalMarks,
    bool CnicMatched,
    bool NameMatched,
    bool BoardMatched,
    bool SeatNoMatched,
    bool PassingYearMatched,
    bool MarksMatched,
    bool DocumentTypeMatched,
    string AuditLog);

public interface IDocumentAuthenticityService
{
    Task<DocumentAuthenticityResult> VerifyAuthenticityAsync(
        DocumentOcrResult ocrResult,
        string fileName,
        string documentType,
        string? candidateFullName,
        string? candidateFatherName,
        string? candidateCnic,
        int? candidateObtainedMarks = null,
        int? candidateTotalMarks = null,
        string? candidateSeatNo = null,
        int? candidatePassingYear = null,
        string? candidateBoard = null,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentAuthenticityService : IDocumentAuthenticityService
{
    private readonly ILogger<DocumentAuthenticityService> _logger;

    public DocumentAuthenticityService(
        ILogger<DocumentAuthenticityService> logger)
    {
        _logger = logger;
    }

    public Task<DocumentAuthenticityResult> VerifyAuthenticityAsync(
        DocumentOcrResult ocrResult,
        string fileName,
        string documentType,
        string? candidateFullName,
        string? candidateFatherName,
        string? candidateCnic,
        int? candidateObtainedMarks = null,
        int? candidateTotalMarks = null,
        string? candidateSeatNo = null,
        int? candidatePassingYear = null,
        string? candidateBoard = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying document authenticity for '{FileName}' ({DocType}) for student '{Student}'",
            fileName, documentType, candidateFullName);

        // 1. Consume the caller-provided OCR extraction (OCR runs only once per upload)
        if (!ocrResult.IsSuccess)
        {
            return Task.FromResult(new DocumentAuthenticityResult(
                IsAuthentic: true,
                Status: "PendingReview",
                AuthenticityScore: 0,
                DocumentType: documentType,
                Reason: ocrResult.Message ?? "OCR could not verify this document. Manual review is required.",
                DetectedCnic: ocrResult.ExtractedCnic,
                DetectedName: ocrResult.ExtractedName,
                DetectedFatherName: ocrResult.ExtractedFatherName,
                DetectedBoard: ocrResult.ExtractedBoard,
                DetectedSeatNo: ocrResult.ExtractedSeatNo,
                DetectedPassingYear: ocrResult.ExtractedPassingYear,
                DetectedObtainedMarks: ocrResult.ExtractedObtainedMarks,
                DetectedTotalMarks: ocrResult.ExtractedTotalMarks,
                CnicMatched: false,
                NameMatched: false,
                BoardMatched: false,
                SeatNoMatched: false,
                PassingYearMatched: false,
                MarksMatched: false,
                DocumentTypeMatched: false,
                AuditLog: "OCR unavailable; document routed for manual review."));
        }

        var lowerFileName = fileName.ToLowerInvariant();
        var lowerDocType = documentType.ToLowerInvariant();

        bool isSuspiciousFile = false;
        string? suspiciousReason = null;

        if (lowerDocType.Contains("cnic"))
        {
            if (lowerFileName.Contains("service_card") || lowerFileName.Contains("employee_card") || 
                lowerFileName.Contains("office_card") || lowerFileName.Contains("atm_card") ||
                lowerFileName.Contains("bank_card"))
            {
                isSuspiciousFile = true;
                suspiciousReason = "Uploaded file appears to be a Service/Employee Card rather than a National CNIC/B-Form.";
            }
        }
        else if (lowerDocType.Contains("intermediate") || lowerDocType.Contains("matric") || lowerDocType.Contains("marks"))
        {
            if (lowerFileName.Contains("service_card") || lowerFileName.Contains("selfie") || lowerFileName.Contains("avatar"))
            {
                isSuspiciousFile = true;
                suspiciousReason = "Uploaded file does not appear to be an educational board marksheet.";
            }
        }

        int score = 100;
        bool cnicMatched = false;
        bool nameMatched = false;
        bool boardMatched = false;
        bool seatNoMatched = false;
        bool passingYearMatched = false;
        bool marksMatched = false;
        bool docTypeMatched = !isSuspiciousFile;

        var auditRemarks = new System.Text.StringBuilder();
        int matchedCount = 0;
        int checkedCount = 0;

        // ==========================================
        // 2. Marksheet Cross-Verification
        // ==========================================
        if (lowerDocType.Contains("intermediate") || lowerDocType.Contains("matric") || lowerDocType.Contains("marks"))
        {
            // Point 1: Candidate Name
            if (!string.IsNullOrWhiteSpace(candidateFullName))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedName))
                {
                    if (DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedName, candidateFullName))
                    {
                        nameMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Name: '{candidateFullName}' verified on marksheet. ");
                    }
                    else
                    {
                        nameMatched = false;
                        score -= 30;
                        auditRemarks.Append($"• NAME MISMATCH: Marksheet reads '{ocrResult.ExtractedName}', but profile has '{candidateFullName}'. ");
                        suspiciousReason ??= $"Name on marksheet ('{ocrResult.ExtractedName}') differs from profile name ('{candidateFullName}').";
                    }
                }
                else
                {
                    nameMatched = true;
                    matchedCount++;
                    auditRemarks.Append($"• Name: '{candidateFullName}' logged. ");
                }
            }

            // Point 2: Father's Name
            if (!string.IsNullOrWhiteSpace(candidateFatherName))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedFatherName))
                {
                    if (DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedFatherName, candidateFatherName))
                    {
                        matchedCount++;
                        auditRemarks.Append($"• Father: '{candidateFatherName}' verified. ");
                    }
                    else
                    {
                        score -= 20;
                        auditRemarks.Append($"• FATHER MISMATCH: Marksheet reads '{ocrResult.ExtractedFatherName}', but profile has '{candidateFatherName}'. ");
                        suspiciousReason ??= $"Father's name on marksheet ('{ocrResult.ExtractedFatherName}') differs from profile father name ('{candidateFatherName}').";
                    }
                }
                else
                {
                    matchedCount++;
                }
            }

            // Point 3: Board Name
            if (!string.IsNullOrWhiteSpace(candidateBoard))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedBoard))
                {
                    if (DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedBoard, candidateBoard) ||
                        ocrResult.ExtractedBoard.Contains(candidateBoard, StringComparison.OrdinalIgnoreCase) ||
                        candidateBoard.Contains(ocrResult.ExtractedBoard, StringComparison.OrdinalIgnoreCase))
                    {
                        boardMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Board: {ocrResult.ExtractedBoard} matched. ");
                    }
                    else
                    {
                        score -= 15;
                        auditRemarks.Append($"• Board: {ocrResult.ExtractedBoard} logged. ");
                    }
                }
                else
                {
                    boardMatched = true;
                    matchedCount++;
                }
            }

            // Point 4: Roll / Seat Number
            if (!string.IsNullOrWhiteSpace(candidateSeatNo))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedSeatNo))
                {
                    var normDocSeat = NormalizeDigits(ocrResult.ExtractedSeatNo);
                    var normFormSeat = NormalizeDigits(candidateSeatNo);
                    if (normDocSeat == normFormSeat || ocrResult.ExtractedSeatNo.Contains(candidateSeatNo) || candidateSeatNo.Contains(ocrResult.ExtractedSeatNo))
                    {
                        seatNoMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Seat #: {candidateSeatNo} verified. ");
                    }
                    else
                    {
                        score -= 15;
                        auditRemarks.Append($"• Seat #: Marksheet reads '{ocrResult.ExtractedSeatNo}'. ");
                    }
                }
                else
                {
                    seatNoMatched = true;
                    matchedCount++;
                }
            }

            // Point 5: Passing Year
            if (candidatePassingYear.HasValue && candidatePassingYear > 2000)
            {
                checkedCount++;
                if (ocrResult.ExtractedPassingYear.HasValue)
                {
                    if (ocrResult.ExtractedPassingYear.Value == candidatePassingYear.Value)
                    {
                        passingYearMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Year: {candidatePassingYear.Value} verified. ");
                    }
                    else
                    {
                        score -= 10;
                        auditRemarks.Append($"• Year: Marksheet reads '{ocrResult.ExtractedPassingYear.Value}'. ");
                    }
                }
                else
                {
                    passingYearMatched = true;
                    matchedCount++;
                }
            }

            // Point 6: Marks Obtained & Total
            if (candidateObtainedMarks.HasValue && candidateTotalMarks.HasValue)
            {
                checkedCount++;
                if (ocrResult.ExtractedObtainedMarks.HasValue && ocrResult.ExtractedTotalMarks.HasValue)
                {
                    if (Math.Abs(ocrResult.ExtractedObtainedMarks.Value - candidateObtainedMarks.Value) <= 10)
                    {
                        marksMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Marks: {candidateObtainedMarks}/{candidateTotalMarks} verified. ");
                    }
                    else
                    {
                        score -= 20;
                        auditRemarks.Append($"• Marks: Marksheet reads {ocrResult.ExtractedObtainedMarks}/{ocrResult.ExtractedTotalMarks}. ");
                    }
                }
                else
                {
                    marksMatched = true;
                    matchedCount++;
                }
            }
        }
        // ==========================================
        // 3. CNIC Document Verification (3-Point Strict Audit)
        // ==========================================
        else if (lowerDocType.Contains("cnic"))
        {
            if (isSuspiciousFile)
            {
                score -= 50;
                auditRemarks.Append("• Non-CNIC file detected. ");
            }

            // Point 1: 13-Digit CNIC Number (40 Points)
            if (!string.IsNullOrWhiteSpace(candidateCnic))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedCnic))
                {
                    var normOcr = NormalizeDigits(ocrResult.ExtractedCnic);
                    var normForm = NormalizeDigits(candidateCnic);
                    if (normOcr == normForm || normOcr.Contains(normForm) || normForm.Contains(normOcr))
                    {
                        cnicMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• CNIC: {candidateCnic} matched identity card. ");
                    }
                    else
                    {
                        cnicMatched = false;
                        score -= 40;
                        auditRemarks.Append($"• CNIC MISMATCH: Card reads '{ocrResult.ExtractedCnic}', entered '{candidateCnic}'. ");
                        suspiciousReason ??= $"CNIC on card ({ocrResult.ExtractedCnic}) differs from entered CNIC ({candidateCnic}).";
                    }
                }
                else
                {
                    cnicMatched = true;
                    matchedCount++;
                    auditRemarks.Append($"• CNIC: {candidateCnic} registered. ");
                }
            }

            // Point 2: Candidate Name on CNIC (35 Points)
            if (!string.IsNullOrWhiteSpace(candidateFullName))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedName))
                {
                    if (DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedName, candidateFullName))
                    {
                        nameMatched = true;
                        matchedCount++;
                        auditRemarks.Append($"• Name: '{candidateFullName}' verified on CNIC card. ");
                    }
                    else
                    {
                        nameMatched = false;
                        score -= 35;
                        auditRemarks.Append($"• NAME MISMATCH: CNIC card reads '{ocrResult.ExtractedName}', but entered name is '{candidateFullName}'. ");
                        suspiciousReason ??= $"Name on CNIC card ('{ocrResult.ExtractedName}') differs from profile name ('{candidateFullName}').";
                    }
                }
                else
                {
                    nameMatched = true;
                    matchedCount++;
                    auditRemarks.Append($"• Name: Verified with profile. ");
                }
            }

            // Point 3: Father's Name on CNIC (25 Points)
            if (!string.IsNullOrWhiteSpace(candidateFatherName))
            {
                checkedCount++;
                if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedFatherName))
                {
                    if (DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedFatherName, candidateFatherName))
                    {
                        matchedCount++;
                        auditRemarks.Append($"• Father Name: '{candidateFatherName}' verified on CNIC card. ");
                    }
                    else
                    {
                        score -= 25;
                        auditRemarks.Append($"• FATHER NAME MISMATCH: CNIC card reads '{ocrResult.ExtractedFatherName}', but entered father name is '{candidateFatherName}'. ");
                        suspiciousReason ??= $"Father's name on CNIC ('{ocrResult.ExtractedFatherName}') differs from profile father name ('{candidateFatherName}').";
                    }
                }
                else
                {
                    matchedCount++;
                }
            }
        }

        score = Math.Clamp(score, 0, 100);

        string status;
        bool isAuthentic;

        if (isSuspiciousFile || score < 50)
        {
            status = "PendingReview";
            isAuthentic = false;
            suspiciousReason ??= "Document failed automated identity/marksheet cross-verification.";
        }
        else if (score >= 75 && (nameMatched || string.IsNullOrWhiteSpace(ocrResult.ExtractedName)))
        {
            status = "Verified";
            isAuthentic = true;
            suspiciousReason = $"Document verified ({matchedCount}/{Math.Max(checkedCount, 1)} data points matched).";
        }
        else
        {
            status = "PendingReview";
            isAuthentic = true;
            suspiciousReason ??= "Document uploaded. Queued for Controller scrutiny.";
        }

        return Task.FromResult(new DocumentAuthenticityResult(
            IsAuthentic: isAuthentic,
            Status: status,
            AuthenticityScore: score,
            DocumentType: documentType,
            Reason: suspiciousReason,
            DetectedCnic: ocrResult.ExtractedCnic,
            DetectedName: ocrResult.ExtractedName,
            DetectedFatherName: ocrResult.ExtractedFatherName,
            DetectedBoard: ocrResult.ExtractedBoard,
            DetectedSeatNo: ocrResult.ExtractedSeatNo,
            DetectedPassingYear: ocrResult.ExtractedPassingYear,
            DetectedObtainedMarks: ocrResult.ExtractedObtainedMarks,
            DetectedTotalMarks: ocrResult.ExtractedTotalMarks,
            CnicMatched: cnicMatched,
            NameMatched: nameMatched,
            BoardMatched: boardMatched,
            SeatNoMatched: seatNoMatched,
            PassingYearMatched: passingYearMatched,
            MarksMatched: marksMatched,
            DocumentTypeMatched: docTypeMatched,
            AuditLog: auditRemarks.Length > 0 ? auditRemarks.ToString().Trim() : "Document verified."
        ));
    }

    private static string NormalizeDigits(string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }
}
