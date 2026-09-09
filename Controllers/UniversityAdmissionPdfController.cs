using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaluExamPortal.Application.UniversityAdmission.Models;
using SaluExamPortal.Application.UniversityAdmission.Services;

namespace SaluExamPortal.Controllers;

[ApiController]
[Route("api/university-admission")]
[Authorize]
public class UniversityAdmissionPdfController : ControllerBase
{
    private readonly IAdmissionPdfService _pdfService;
    private readonly IQrCodeService _qrService;
    private readonly UniversityAdmissionApplicationStore _applicationStore;
    private readonly AdmissionFormService _formService;
    private readonly ILogger<UniversityAdmissionPdfController> _logger;

    public UniversityAdmissionPdfController(
        IAdmissionPdfService pdfService,
        IQrCodeService qrService,
        UniversityAdmissionApplicationStore applicationStore,
        AdmissionFormService formService,
        ILogger<UniversityAdmissionPdfController> logger)
    {
        _pdfService = pdfService;
        _qrService = qrService;
        _applicationStore = applicationStore;
        _formService = formService;
        _logger = logger;
    }

    /// <summary>
    /// Download the official Shah Abdul Latif University Admission Application Form as PDF
    /// </summary>
    [HttpGet("download-form")]
    [HttpGet("download-form/{trackingId}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DownloadApplicationForm(string? trackingId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (form, effectiveTrackingId) = await ResolveFormAndTrackingId(userId, trackingId);

            var verificationUrl = $"{Request.Scheme}://{Request.Host}/verify/admission/{effectiveTrackingId}";
            var pdfBytes = _pdfService.GenerateApplicationFormPdf(form, effectiveTrackingId, verificationUrl);

            return File(pdfBytes, "application/pdf", $"SALU-Admission-Form-{effectiveTrackingId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating admission form PDF");
            return StatusCode(500, "An error occurred while generating the PDF document.");
        }
    }

    /// <summary>
    /// Download the 3-part bank fee challan (Bank Copy, University Copy, Student Copy)
    /// </summary>
    [HttpGet("download-challan")]
    [HttpGet("download-challan/{trackingId}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DownloadFeeChallan(string? trackingId = null, [FromQuery] decimal amount = 2500m)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (form, effectiveTrackingId) = await ResolveFormAndTrackingId(userId, trackingId);

            var pdfBytes = _pdfService.GenerateAdmissionChallanPdf(form, effectiveTrackingId, amount);

            return File(pdfBytes, "application/pdf", $"SALU-Fee-Challan-{effectiveTrackingId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating admission challan PDF");
            return StatusCode(500, "An error occurred while generating the fee challan.");
        }
    }

    /// <summary>
    /// Generate a standalone verification QR Code image
    /// </summary>
    [HttpGet("qr-code")]
    [AllowAnonymous]
    [Produces("image/png")]
    public IActionResult GenerateQrCode([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Code parameter is required.");

        var pngBytes = _qrService.GenerateQrCodePng(code, 12);
        return File(pngBytes, "image/png");
    }

    private async Task<(AdmissionFormState Form, string TrackingId)> ResolveFormAndTrackingId(string? userId, string? trackingId)
    {
        AdmissionFormState? form = null;
        var effectiveTrackingId = trackingId;

        // Try load from DB first if user is authenticated
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var savedApp = await _applicationStore.GetByUserIdAsync(userId);
            if (savedApp != null && !string.IsNullOrWhiteSpace(savedApp.FormStateJson))
            {
                try
                {
                    var state = JsonSerializer.Deserialize<AdmissionFormState>(savedApp.FormStateJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (state != null)
                    {
                        form = state;
                        if (string.IsNullOrWhiteSpace(effectiveTrackingId))
                        {
                            effectiveTrackingId = savedApp.ApplicationNumber;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize saved application JSON for user {UserId}", userId);
                }
            }
        }

        // Fallback to active in-memory session if available
        if (form == null && _formService.State != null)
        {
            form = _formService.State;
        }

        // Fallback to empty model if not yet filled
        form ??= new AdmissionFormState();

        if (string.IsNullOrWhiteSpace(effectiveTrackingId))
        {
            effectiveTrackingId = $"SALU-2026-{Random.Shared.Next(10000, 99999)}";
        }

        return (form, effectiveTrackingId);
    }
}
