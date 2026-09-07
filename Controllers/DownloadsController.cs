using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Controllers;

/// <summary>
/// Authorized file download endpoint for secure access to student documents.
/// All requests are verified for user ownership and authorization before file serving.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication
public class DownloadsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DownloadsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DownloadsController(
        ApplicationDbContext dbContext,
        ILogger<DownloadsController> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Download a file if user owns the associated enrollment.
    /// Verifies ownership before returning file stream.
    /// </summary>
    [HttpGet("{fileId}")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadFile(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            return BadRequest("File ID is required");

        try
        {
            // Get current user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User not authenticated");

            // Verify user owns this document
            // Look for fileId in user's enrollments (either in DocumentsJson or photo)
            var enrollment = await _dbContext.Enrollments
                .Where(e => e.UserId == userId && (
                    e.PhotoUrl!.Contains(fileId) ||
                    e.DocumentsJson!.Contains(fileId)))
                .FirstOrDefaultAsync();

            if (enrollment == null)
            {
                _logger.LogWarning(
                    "[ACCESS DENIED]: User '{UserId}' attempted unauthorized download of file '{FileId}'",
                    userId, fileId);
                return Forbid();
            }

            // Sanitize path to prevent directory traversal
            var safeFileName = Path.GetFileName(fileId);
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads");
            
            // Determine subdirectory (documents or photos)
            string fullPath;
            if (enrollment.PhotoUrl?.Contains(safeFileName) == true)
            {
                fullPath = Path.Combine(uploadsRoot, "photos", safeFileName);
            }
            else
            {
                fullPath = Path.Combine(uploadsRoot, "documents", safeFileName);
            }

            // Verify resolved path is within uploads directory (no traversal attacks)
            var fullPathNormalized = Path.GetFullPath(fullPath);
            var uploadsPathNormalized = Path.GetFullPath(uploadsRoot);
            
            if (!fullPathNormalized.StartsWith(uploadsPathNormalized, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "[SECURITY]: Path traversal attempt detected for file '{FileId}' by user '{UserId}'",
                    fileId, userId);
                return NotFound();
            }

            // Check file exists
            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("File not found: {FilePath}", fullPath);
                return NotFound("File not found");
            }

            // Return file with security headers
            var fileStream = System.IO.File.OpenRead(fullPath);
            var fileName = Path.GetFileNameWithoutExtension(safeFileName);
            var extension = Path.GetExtension(safeFileName);

            _logger.LogInformation(
                "[FILE DOWNLOAD]: User '{UserId}' downloaded file '{FileName}'",
                userId, safeFileName);

            return File(
                fileStream,
                GetMimeType(extension),
                fileDownloadName: fileName + extension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileId);
            return StatusCode(500, "Internal server error");
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
