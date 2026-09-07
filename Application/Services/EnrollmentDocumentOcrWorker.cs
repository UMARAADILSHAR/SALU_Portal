using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public sealed class EnrollmentDocumentOcrWorker(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<EnrollmentDocumentOcrWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Enrollment document OCR worker failed while processing a batch.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var ocr = scope.ServiceProvider.GetRequiredService<IDocumentOcrService>();
        var authenticity = scope.ServiceProvider.GetRequiredService<IDocumentAuthenticityService>();

        var documents = await db.EnrollmentDocuments
            .Include(document => document.Enrollment)
            .Where(document => document.ProcessingStatus == "Queued" && document.ProcessingAttempts < 3)
            .OrderBy(document => document.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var document in documents)
        {
            if (document.Enrollment.Status != Domain.Enums.EnrollmentStatus.Draft
                && document.Enrollment.Status != Domain.Enums.EnrollmentStatus.Rejected)
            {
                document.ProcessingStatus = "Locked";
                document.LastError = "Enrollment was submitted before OCR processing completed.";
                continue;
            }

            document.ProcessingStatus = "Processing";
            document.ProcessingAttempts++;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var fullPath = GetPhysicalPath(document.RelativePath);
                await using var stream = File.OpenRead(fullPath);
                var ocrResult = await ocr.AnalyzeDocumentAsync(stream, document.FileName, document.DocumentType, cancellationToken);

                var records = JsonSerializer.Deserialize<List<AcademicRecordItem>>(document.Enrollment.AcademicRecordsJson ?? "[]") ?? [];
                var academicRecord = records.FirstOrDefault(record =>
                    (document.DocumentType.Contains("MATRIC", StringComparison.OrdinalIgnoreCase) && record.Qualification.Contains("MATRIC", StringComparison.OrdinalIgnoreCase))
                    || (document.DocumentType.Contains("INTER", StringComparison.OrdinalIgnoreCase) && record.Qualification.Contains("INTER", StringComparison.OrdinalIgnoreCase)));

                var verification = await authenticity.VerifyAuthenticityAsync(
                    ocrResult,
                    document.FileName,
                    document.DocumentType,
                    await GetCandidateNameAsync(scope, document.Enrollment.UserId, cancellationToken),
                    document.Enrollment.FatherName,
                    await GetCandidateCnicAsync(scope, document.Enrollment.UserId, cancellationToken),
                    academicRecord?.MarksObtained,
                    academicRecord?.TotalMarks,
                    academicRecord?.SeatNo,
                    academicRecord?.PassingYear,
                    academicRecord?.BoardUniversity ?? document.Enrollment.BoardName,
                    cancellationToken: cancellationToken);

                document.OcrResultJson = JsonSerializer.Serialize(ocrResult);
                document.AuthenticityResultJson = JsonSerializer.Serialize(verification);
                document.ProcessingStatus = ocrResult.IsSuccess ? "Verified" : "NeedsReview";
                document.LastError = null;
                document.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                document.ProcessingStatus = document.ProcessingAttempts >= 3 ? "Failed" : "Queued";
                document.LastError = ex.Message;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning(ex, "OCR failed for enrollment document {DocumentId}.", document.Id);
            }
        }

        if (documents.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private string GetPhysicalPath(string relativePath)
    {
        var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(environment.ContentRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document path is outside the upload directory.");
        return fullPath;
    }

    private static async Task<string?> GetCandidateNameAsync(IServiceScope scope, string userId, CancellationToken cancellationToken)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        return user?.FullName;
    }

    private static async Task<string?> GetCandidateCnicAsync(IServiceScope scope, string userId, CancellationToken cancellationToken)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        return user?.Cnic;
    }
}
