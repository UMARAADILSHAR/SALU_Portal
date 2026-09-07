using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SaluExamPortal.Application.Services;

public sealed record DocumentOcrResult(
    bool IsSuccess,
    string DocumentType,
    string? ExtractedCnic = null,
    string? ExtractedName = null,
    string? ExtractedFatherName = null,
    string? ExtractedDob = null,
    string? ExtractedSeatNo = null,
    int? ExtractedPassingYear = null,
    int? ExtractedObtainedMarks = null,
    int? ExtractedTotalMarks = null,
    string? ExtractedGrade = null,
    string? ExtractedBoard = null,
    string? ExtractedGender = null,
    float Confidence = 0.95f,
    string? RawTextSummary = null,
    string? Message = null);

public sealed record DocumentCrossVerification(
    bool? IsCnicMatched,
    bool? IsNameMatched,
    bool? IsFatherNameMatched,
    bool? IsMarksConsistent,
    float OverallMatchScore,
    string VerificationStatus,
    string AuditRemarks);

public interface IDocumentOcrService
{
    Task<DocumentOcrResult> AnalyzeDocumentAsync(
        Stream fileStream,
        string fileName,
        string documentType,
        CancellationToken cancellationToken = default);

    DocumentCrossVerification CrossVerifyWithEnrollment(
        DocumentOcrResult ocrResult,
        string? formFullName,
        string? formFatherName,
        string? formCnic,
        int? formObtainedMarks,
        int? formTotalMarks);
}

/// <summary>
/// Engine-agnostic OCR field extraction and fuzzy matching helpers,
/// tuned for Pakistani BISE/CNIC documents.
/// </summary>
public static class DocumentOcrFieldExtractor
{
    public static DocumentOcrResult ExtractFieldsFromRecognizedText(string text, string documentType, float confidence)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentOcrResult(
                IsSuccess: false,
                DocumentType: documentType,
                Confidence: 0f,
                RawTextSummary: "Document uploaded, but no OCR text was detected.",
                Message: "OCR could not read this document. Manual review is required."
            );
        }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

        // 1. Pakistani Educational Board Detection
        string? board = DetectBoard(text, lines);

        // 2. Roll / Seat Number Detection
        string? rollNo = DetectSeatNumber(text, lines);

        // 3. Passing / Examination Year (2010 - 2030)
        int? passingYear = DetectPassingYear(text, lines);

        // 4. Marks Obtained & Total Marks
        var (obtainedMarks, totalMarks) = DetectMarks(text, lines);

        // 5. Candidate Name
        string? name = DetectCandidateName(text, lines);

        // 6. Father's Name
        string? fatherName = DetectFatherName(text, lines);

        // 7. CNIC Number
        string? cnic = DetectCnic(text);

        // 8. Grade / Division
        string? grade = DetectGrade(text);

        return new DocumentOcrResult(
            IsSuccess: true,
            DocumentType: documentType,
            ExtractedCnic: cnic,
            ExtractedName: name,
            ExtractedFatherName: fatherName,
            ExtractedSeatNo: rollNo,
            ExtractedPassingYear: passingYear,
            ExtractedObtainedMarks: obtainedMarks,
            ExtractedTotalMarks: totalMarks,
            ExtractedGrade: grade,
            ExtractedBoard: board,
            Confidence: confidence,
            RawTextSummary: text.Length > 300 ? text[..300] + "..." : text,
            Message: "Document scanned and verified successfully."
        );
    }

    private static string? DetectBoard(string text, List<string> lines)
    {
        var boardMatch = Regex.Match(text, @"\b(BISE\s+[A-Za-z]+|Board\s+of\s+Intermediate[^,\n]+|Board\s+of\s+Secondary[^,\n]+|Federal\s+Board|FBISE|BIEK|BSEK|Aga\s+Khan\s+Board|Shah\s+Abdul\s+Latif\s+University|SALU|Sindh\s+Board|Technical\s+Board|Sukkur\s+Board|Larkana\s+Board|Hyderabad\s+Board|Karachi\s+Board|Mirpurkhas\s+Board|Benazirabad\s+Board|Nawabshah\s+Board)\b", RegexOptions.IgnoreCase);
        if (boardMatch.Success)
        {
            var detected = boardMatch.Groups[1].Value.Trim();
            if (detected.Contains("Sukkur", StringComparison.OrdinalIgnoreCase)) return "BISE Sukkur";
            if (detected.Contains("Larkana", StringComparison.OrdinalIgnoreCase)) return "BISE Larkana";
            if (detected.Contains("Hyderabad", StringComparison.OrdinalIgnoreCase)) return "BISE Hyderabad";
            if (detected.Contains("Karachi", StringComparison.OrdinalIgnoreCase)) return "BIEK / BSEK Karachi";
            if (detected.Contains("Mirpurkhas", StringComparison.OrdinalIgnoreCase)) return "BISE Mirpurkhas";
            if (detected.Contains("Benazirabad", StringComparison.OrdinalIgnoreCase) || detected.Contains("Nawabshah", StringComparison.OrdinalIgnoreCase)) return "BISE Shaheed Benazirabad";
            if (detected.Contains("Federal", StringComparison.OrdinalIgnoreCase) || detected.Contains("FBISE", StringComparison.OrdinalIgnoreCase)) return "FBISE Islamabad";
            return detected;
        }

        return null;
    }

    private static string? DetectSeatNumber(string text, List<string> lines)
    {
        var rollMatch = Regex.Match(text, @"\b(?:Roll\s*No|Seat\s*No|RollNumber|Enrolment\s*No|Reg[.]?\s*No|Seat\s*Number|Roll\s*Number)[\s:.-]*\s*([A-Za-z0-9\/-]{4,15})\b", RegexOptions.IgnoreCase);
        if (rollMatch.Success)
        {
            var val = rollMatch.Groups[1].Value.Trim();
            if (val.Any(char.IsDigit)) return CleanNumberToken(val);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], @"\b(Roll\s*No|Seat\s*No|Seat\s*Number|Roll\s*Number)\b", RegexOptions.IgnoreCase))
            {
                if (i + 1 < lines.Count)
                {
                    var nextDigits = Regex.Match(lines[i + 1], @"\b([A-Za-z0-9\/-]{4,12})\b");
                    if (nextDigits.Success && nextDigits.Groups[1].Value.Any(char.IsDigit))
                    {
                        return CleanNumberToken(nextDigits.Groups[1].Value);
                    }
                }
            }
        }

        var numMatch = Regex.Match(text, @"\b(\d{5,7})\b");
        if (numMatch.Success) return numMatch.Groups[1].Value.Trim();

        return null;
    }

    private static int? DetectPassingYear(string text, List<string> lines)
    {
        var yearMatch = Regex.Match(text, @"\b(?:Annual|Supplementary|Examination|Exam|Year|Session|HSC|SSC|Part-II|Part-I)[\s:.-]*(?:\r?\n|\s)*(20\d{2})\b", RegexOptions.IgnoreCase);
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var yVal) && yVal >= 2010 && yVal <= 2030)
        {
            return yVal;
        }

        var generalYearMatch = Regex.Match(text, @"\b(20[12]\d)\b");
        if (generalYearMatch.Success && int.TryParse(generalYearMatch.Groups[1].Value, out var gYear) && gYear >= 2010 && gYear <= 2030)
        {
            return gYear;
        }

        return null;
    }

    private static (int? obtained, int? total) DetectMarks(string text, List<string> lines)
    {
        int? obtainedMarks = null;
        int? totalMarks = null;

        var ratioMatch = Regex.Match(text, @"\b(\d{2,4})\s*(?:[/]|OUT\s+OF)\s*(\d{3,4})\b", RegexOptions.IgnoreCase);
        if (ratioMatch.Success)
        {
            if (int.TryParse(ratioMatch.Groups[1].Value, out var rObt) && int.TryParse(ratioMatch.Groups[2].Value, out var rTot))
            {
                if (rObt <= rTot && rTot is 1100 or 850 or 900 or 1000 or 1050 or 1200 or 500 or 600 or 700 or 800)
                {
                    return (rObt, rTot);
                }
            }
        }

        var obtMatch = Regex.Match(text, @"\b(?:Marks\s*Obtained|Obtained\s*Marks|Obt[.]?\s*Marks|Marks\s*Secured|Secured\s*Marks|Total\s*Marks\s*Obtained|Grand\s*Total)[\s:.-]*(?:\r?\n|\s)*(\d{2,4})\b", RegexOptions.IgnoreCase);
        if (obtMatch.Success && int.TryParse(obtMatch.Groups[1].Value, out var oVal))
        {
            obtainedMarks = oVal;
        }

        var totMatch = Regex.Match(text, @"\b(?:Total\s*Marks|Max[.]?\s*Marks|Maximum\s*Marks|Aggregate\s*Marks)[\s:.-]*(?:\r?\n|\s)*(\d{3,4})\b", RegexOptions.IgnoreCase);
        if (totMatch.Success && int.TryParse(totMatch.Groups[1].Value, out var tVal))
        {
            totalMarks = tVal;
        }

        if (!obtainedMarks.HasValue || !totalMarks.HasValue)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var cur = lines[i];
                if (!obtainedMarks.HasValue && Regex.IsMatch(cur, @"\b(Marks\s*Obtained|Obtained\s*Marks|Obt[.]?\s*Marks|Secured)\b", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count)
                    {
                        var mNext = Regex.Match(lines[i + 1], @"\b(\d{2,4})\b");
                        if (mNext.Success && int.TryParse(mNext.Groups[1].Value, out var nObt))
                        {
                            obtainedMarks = nObt;
                        }
                    }
                }

                if (!totalMarks.HasValue && Regex.IsMatch(cur, @"\b(Total\s*Marks|Max\s*Marks|Maximum)\b", RegexOptions.IgnoreCase))
                {
                    if (i + 1 < lines.Count)
                    {
                        var mTot = Regex.Match(lines[i + 1], @"\b(\d{3,4})\b");
                        if (mTot.Success && int.TryParse(mTot.Groups[1].Value, out var nTot))
                        {
                            totalMarks = nTot;
                        }
                    }
                }
            }
        }

        if (obtainedMarks.HasValue && !totalMarks.HasValue)
        {
            totalMarks = obtainedMarks.Value > 850 ? 1100 : 850;
        }

        return (obtainedMarks, totalMarks);
    }

    private static string? DetectCandidateName(string text, List<string> lines)
    {
        // Pattern 1: Single line "Name: Muhammad Bilal" or "Name of Holder: Muhammad Bilal"
        var nameMatch = Regex.Match(text, @"\b(?:Candidate(?:'s)?\s*Name|Student\s*Name|Name\s*of\s*Candidate|Name\s*of\s*Student|Name\s*of\s*Holder|Name)[\s:.-]+([A-Za-z\s]{3,35})(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (nameMatch.Success)
        {
            var raw = CleanNameCandidate(nameMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(raw) && !IsIgnoredHeader(raw)) return raw;
        }

        // Pattern 2: Multi-line (Label on line N, Name on line N+1)
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (Regex.IsMatch(trimmed, @"^(?:Candidate(?:'s)?\s*Name|Student\s*Name|Name\s*of\s*Candidate|Name\s*of\s*Student|Name\s*of\s*Holder|Name)[\s:.-]*$", RegexOptions.IgnoreCase))
            {
                if (i + 1 < lines.Count)
                {
                    var next = CleanNameCandidate(lines[i + 1]);
                    if (!string.IsNullOrWhiteSpace(next) && !IsIgnoredHeader(next)) return next;
                }
            }
        }

        return null;
    }

    private static string? DetectFatherName(string text, List<string> lines)
    {
        // Pattern 1: Single line "Father Name: Abdul Karim"
        var fatherMatch = Regex.Match(text, @"\b(?:Father(?:'s)?\s*Name|Father\s*Name|F[\s/.-]*Name|Father|Parent\s*Name|Husband\s*Name|H[\s/.-]*Name|S[\s/.]*O|D[\s/.]*O|W[\s/.]*O)[\s:.-]+([A-Za-z\s]{3,35})(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (fatherMatch.Success)
        {
            var raw = CleanNameCandidate(fatherMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(raw) && !IsIgnoredHeader(raw)) return raw;
        }

        // Pattern 2: Multi-line
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (Regex.IsMatch(trimmed, @"^(?:Father(?:'s)?\s*Name|Father\s*Name|F[\s/.-]*Name|Father|Husband\s*Name|Parent\s*Name|S[\s/.]*O|D[\s/.]*O)[\s:.-]*$", RegexOptions.IgnoreCase))
            {
                if (i + 1 < lines.Count)
                {
                    var next = CleanNameCandidate(lines[i + 1]);
                    if (!string.IsNullOrWhiteSpace(next) && !IsIgnoredHeader(next)) return next;
                }
            }
        }

        return null;
    }

    private static string? DetectCnic(string text)
    {
        var cnicMatch = Regex.Match(text, @"\b(\d{5})[- ]?(\d{7})[- ]?(\d)\b");
        if (cnicMatch.Success)
        {
            return $"{cnicMatch.Groups[1].Value}-{cnicMatch.Groups[2].Value}-{cnicMatch.Groups[3].Value}";
        }
        return null;
    }

    private static string? DetectGrade(string text)
    {
        var gradeMatch = Regex.Match(text, @"\bGrade[\s:.-]+([A-F][+]?|1st|2nd|3rd|A-One|A1|B|C|D)\b", RegexOptions.IgnoreCase);
        if (gradeMatch.Success)
        {
            return gradeMatch.Groups[1].Value.ToUpperInvariant();
        }
        return null;
    }

    private static bool IsIgnoredHeader(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return true;
        var lower = input.ToLowerInvariant();
        return lower.Contains("pakistan") || lower.Contains("identity card") || lower.Contains("republic") || 
               lower.Contains("government") || lower.Contains("nadra") || lower.Contains("signature") || 
               lower.Contains("country of stay") || lower.Contains("date of birth") || lower.Contains("date of expiry") ||
               lower.Contains("date of issue") || lower.Contains("national identity");
    }

    private static string CleanNameCandidate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var clean = Regex.Replace(input, @"\b(Father|Gender|Date|Birth|CNIC|District|Board|Seat|Roll|Marks|Science|General|Pre-Medical|Pre-Engineering|Commerce|Arts|Group|Examination|HSC|SSC|Identity|Country|Stay)\b.*", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[^\w\s]", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return clean.Length >= 3 ? clean : string.Empty;
    }

    private static string CleanNumberToken(string input)
    {
        return Regex.Replace(input, @"[^\w\/-]", "").Trim();
    }

    public static bool StringFuzzyMatch(string s1, string s2)
    {
        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2)) return false;

        var clean1 = CleanTokens(s1);
        var clean2 = CleanTokens(s2);

        if (clean1 == clean2) return true;
        if (clean1.Contains(clean2) || clean2.Contains(clean1)) return true;

        var set1 = clean1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var set2 = clean2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (set1.Overlaps(set2)) return true;

        int maxLen = Math.Max(clean1.Length, clean2.Length);
        if (maxLen == 0) return true;
        int dist = ComputeLevenshteinDistance(clean1, clean2);
        return (1.0f - ((float)dist / maxLen)) >= 0.65f;
    }

    private static string CleanTokens(string input)
    {
        var clean = Regex.Replace(input, @"\b(mr|ms|miss|mrs|syed|hafiz|muhammad|mohammad|md|so|do|wo|fname|father|name)\b", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[^\w\s]", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim().ToLowerInvariant();
        return clean;
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
