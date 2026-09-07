using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Application.Services;

public interface IEnrollmentCorrectionService
{
    Task<Guid> AuthorizeAllFieldsAsync(Guid enrollmentId, ClaimsPrincipal administrator, string justification, CancellationToken cancellationToken = default);
    Task<bool> HasApprovedCorrectionAsync(Guid enrollmentId, string studentUserId, CancellationToken cancellationToken = default);
}

public sealed class EnrollmentCorrectionService(IApplicationDbContext db) : IEnrollmentCorrectionService
{
    private static readonly string[] AllEnrollmentFields =
    [
        "AcademicYearId", "CollegeId", "Program", "Session", "Semester", "FatherName", "Surname", "SoDoWo",
        "DateOfBirth", "Gender", "Address", "City", "ContactNumber", "DivisionObtained", "PassingYear",
        "BoardName", "ExamFromSalu", "SaluSeatNo", "SaluExamYear", "EligibilityCertNo", "Nationality",
        "Religion", "DomicileProvince", "DomicileDistrict", "MigrationProvince", "MigrationDistrict",
        "PhotoUrl", "AcademicRecordsJson", "DocumentsJson"
    ];

    public async Task<Guid> AuthorizeAllFieldsAsync(Guid enrollmentId, ClaimsPrincipal administrator, string justification, CancellationToken cancellationToken = default)
    {
        if (!administrator.IsInRole(nameof(PortalRole.SuperAdmin)) && !administrator.IsInRole(nameof(PortalRole.Admin)))
            throw new UnauthorizedAccessException("Only SuperAdmin or Admin may authorize enrollment corrections.");

        if (string.IsNullOrWhiteSpace(justification))
            throw new InvalidOperationException("A correction justification is required.");

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment was not found.");

        if (enrollment.Status != EnrollmentStatus.Rejected)
            throw new InvalidOperationException("Only rejected enrollments can be opened for correction.");

        var administratorId = administrator.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Administrator identity is missing.");

        var request = new EnrollmentCorrectionRequest
        {
            EnrollmentId = enrollmentId,
            RequestedBy = administratorId,
            AllowedFieldsJson = JsonSerializer.Serialize(AllEnrollmentFields),
            Justification = justification.Trim(),
            Status = "Approved",
            ReviewedBy = administratorId,
            ReviewedAt = DateTime.UtcNow
        };

        db.EnrollmentCorrectionRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return request.Id;
    }

    public Task<bool> HasApprovedCorrectionAsync(Guid enrollmentId, string studentUserId, CancellationToken cancellationToken = default)
    {
        return db.EnrollmentCorrectionRequests
            .AnyAsync(request => request.EnrollmentId == enrollmentId
                && request.Enrollment.UserId == studentUserId
                && request.Enrollment.Status == EnrollmentStatus.Rejected
                && request.Status == "Approved", cancellationToken);
    }
}
