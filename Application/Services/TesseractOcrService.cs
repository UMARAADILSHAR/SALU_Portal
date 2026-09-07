using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace SaluExamPortal.Application.Services;

public sealed class TesseractOcrService : IDocumentOcrService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly string _tessDataPath;

    public TesseractOcrService(IWebHostEnvironment env, ILogger<TesseractOcrService> logger)
    {
        _env = env;
        _logger = logger;

        // Locate tessdata directory
        var baseTessData = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (Directory.Exists(baseTessData) && File.Exists(Path.Combine(baseTessData, "eng.traineddata")))
        {
            _tessDataPath = baseTessData;
        }
        else
        {
            _tessDataPath = Path.Combine(_env.ContentRootPath, "tessdata");
        }

        _logger.LogInformation("TesseractOcrService initialized with tessdata path: '{Path}'", _tessDataPath);
    }

    public async Task<DocumentOcrResult> AnalyzeDocumentAsync(
        Stream fileStream,
        string fileName,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running Tesseract 5.2 LSTM Neural OCR on '{FileName}' for document type '{DocType}'", fileName, documentType);

        try
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
            var imageBytes = ms.ToArray();

            if (imageBytes.Length == 0)
            {
                return FallbackAnalysis(fileName, documentType);
            }

            if (!Directory.Exists(_tessDataPath) || !File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")))
            {
                _logger.LogWarning("Tesseract language file 'eng.traineddata' not found in '{Path}'.", _tessDataPath);
                return FallbackAnalysis(fileName, documentType);
            }

            string recognizedText = string.Empty;
            float confidence = 0.95f;

            // Execute Tesseract Engine in Task.Run for non-blocking execution
            await Task.Run(() =>
            {
                using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                
                // Allow comprehensive character set for educational documents
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-/:., ()#%&'\"\n\r");
                
                using var pix = Pix.LoadFromMemory(imageBytes);
                if (pix != null)
                {
                    // Pass 1: Auto layout segmentation
                    using var page = engine.Process(pix, PageSegMode.Auto);
                    recognizedText = page.GetText() ?? string.Empty;
                    confidence = Math.Clamp(page.GetMeanConfidence(), 0.50f, 0.99f);

                    // Pass 2: If text is sparse or short, re-process with SparseText mode
                    if (recognizedText.Trim().Length < 30)
                    {
                        using var pageSparse = engine.Process(pix, PageSegMode.SparseText);
                        var sparseText = pageSparse.GetText() ?? string.Empty;
                        if (sparseText.Length > recognizedText.Length)
                        {
                            recognizedText = sparseText;
                            confidence = Math.Clamp(pageSparse.GetMeanConfidence(), 0.50f, 0.99f);
                        }
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("Tesseract recognized {Length} chars with {Confidence:P0} confidence from '{FileName}'",
                recognizedText.Length, confidence, fileName);

            return DocumentOcrFieldExtractor.ExtractFieldsFromRecognizedText(recognizedText, documentType, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract OCR encountered an issue on '{FileName}'. Falling back to structured inspection.", fileName);
            return FallbackAnalysis(fileName, documentType);
        }
    }

    private static DocumentOcrResult FallbackAnalysis(string fileName, string documentType)
    {
        return new DocumentOcrResult(
            IsSuccess: false,
            DocumentType: documentType,
            Confidence: 0f,
            RawTextSummary: $"Document attachment '{fileName}' could not be analyzed.",
            Message: "OCR could not analyze this file. Manual review is required."
        );
    }

    public DocumentCrossVerification CrossVerifyWithEnrollment(
        DocumentOcrResult ocrResult,
        string? formFullName,
        string? formFatherName,
        string? formCnic,
        int? formObtainedMarks,
        int? formTotalMarks)
    {
        bool? cnicMatched = null;
        bool? nameMatched = null;
        bool? fatherMatched = null;
        bool? marksConsistent = null;

        int scoreNumerator = 0;
        int scoreDenominator = 0;

        if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedCnic) && !string.IsNullOrWhiteSpace(formCnic))
        {
            scoreDenominator++;
            var normOcr = new string(ocrResult.ExtractedCnic.Where(char.IsDigit).ToArray());
            var normForm = new string(formCnic.Where(char.IsDigit).ToArray());
            cnicMatched = normOcr == normForm;
            if (cnicMatched.Value) scoreNumerator++;
        }

        if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedName) && !string.IsNullOrWhiteSpace(formFullName))
        {
            scoreDenominator++;
            nameMatched = DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedName, formFullName);
            if (nameMatched.Value) scoreNumerator++;
        }

        if (!string.IsNullOrWhiteSpace(ocrResult.ExtractedFatherName) && !string.IsNullOrWhiteSpace(formFatherName))
        {
            scoreDenominator++;
            fatherMatched = DocumentOcrFieldExtractor.StringFuzzyMatch(ocrResult.ExtractedFatherName, formFatherName);
            if (fatherMatched.Value) scoreNumerator++;
        }

        if (ocrResult.ExtractedObtainedMarks.HasValue && formObtainedMarks.HasValue)
        {
            scoreDenominator++;
            marksConsistent = Math.Abs(ocrResult.ExtractedObtainedMarks.Value - formObtainedMarks.Value) <= 10;
            if (marksConsistent.Value) scoreNumerator++;
        }

        float overallScore = scoreDenominator > 0 ? (float)scoreNumerator / scoreDenominator : 1.0f;
        string status = overallScore >= 0.60f ? "Verified" : (scoreDenominator == 0 ? "Pending Scrutiny" : "Flagged for Manual Review");

        return new DocumentCrossVerification(
            IsCnicMatched: cnicMatched,
            IsNameMatched: nameMatched,
            IsFatherNameMatched: fatherMatched,
            IsMarksConsistent: marksConsistent,
            OverallMatchScore: overallScore,
            VerificationStatus: status,
            AuditRemarks: $"Tesseract OCR Match: {scoreNumerator}/{scoreDenominator} parameters verified."
        );
    }
}
