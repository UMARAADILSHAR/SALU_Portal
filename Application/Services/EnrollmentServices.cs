using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Application.Services;

public sealed record AcademicRecordItem(
    int SNo,
    string Qualification,
    string Degree,
    string SeatNo,
    int PassingYear,
    int MarksObtained,
    int TotalMarks,
    string GradeDivision,
    string BoardUniversity);

public sealed record FeeBreakdown(
    decimal BaseFee,
    decimal MigrationFee,
    decimal LateFee,
    decimal TotalFee,
    bool IsNonAffiliatedDistrict,
    bool IsLateFeeApplied,
    string StatusMessage);

public sealed record EnrollmentSubmission(
    string UserId,
    Guid AcademicYearId,
    Guid? CollegeId,
    string Program,
    string FullName,
    string FatherName,
    DateTime DateOfBirth,
    Gender Gender,
    string Address,
    string DomicileDistrict,
    string DomicileProvince,
    string? Surname = null,
    string? SoDoWo = null,
    string? Session = null,
    string? ContactNumber = null,
    string? Cnic = null,
    string? DivisionObtained = null,
    int? PassingYear = null,
    string? BoardName = null,
    string? ExamFromSalu = null,
    string? SaluSeatNo = null,
    string? SaluExamYear = null,
    string? EligibilityCertNo = null,
    string? Nationality = null,
    string? Religion = null,
    string? MigrationProvince = null,
    string? MigrationDistrict = null,
    string? PhotoUrl = null,
    string? AcademicRecordsJson = null,
    string? DocumentsJson = null
);

public interface IFeeCalculationService
{
    Task<FeeBreakdown> CalculateAsync(string domicileDistrict, CancellationToken cancellationToken = default);
}

public interface IRollNumberService
{
    Task<string> GenerateAsync(Gender gender, CancellationToken cancellationToken = default);
}

public interface IEnrollmentService
{
    Task<Enrollment> SaveDraftAsync(EnrollmentSubmission submission, CancellationToken cancellationToken = default);
    Task<Enrollment> SubmitAsync(EnrollmentSubmission submission, CancellationToken cancellationToken = default);
}

public sealed class FeeCalculationService(IServiceScopeFactory scopeFactory) : IFeeCalculationService
{
    private static readonly HashSet<string> AffiliatedDistricts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sukkur", "Khairpur", "Ghotki", "Shikarpur", "Jacobabad", "Kandhkot", "Kashmore",
        "Larkana", "Shahdadkot", "Qambar-Shahdadkot", "Qambar Shahdadkot", "Naushahro Feroze", "Dadu"
    };

    public async Task<FeeBreakdown> CalculateAsync(string domicileDistrict, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigurationService>();

        var windowStatus = await configService.GetEnrollmentWindowStatusAsync(cancellationToken);
        var settings = await configService.GetEnrollmentSettingsAsync(cancellationToken);

        var baseFee = windowStatus.ApplicableBaseFee;
        var lateFee = windowStatus.ApplicableLateFee;
        var nonAffiliated = string.IsNullOrWhiteSpace(domicileDistrict) || !AffiliatedDistricts.Contains(domicileDistrict.Trim());
        var migrationFee = nonAffiliated ? settings.MigrationNocFee : 0m;
        var totalFee = baseFee + migrationFee + lateFee;

        return new FeeBreakdown(
            baseFee,
            migrationFee,
            lateFee,
            totalFee,
            nonAffiliated,
            windowStatus.IsLateFeeApplied,
            windowStatus.StatusDetailMessage);
    }
}

public sealed class RollNumberService(IServiceScopeFactory scopeFactory) : IRollNumberService
{
    public async Task<string> GenerateAsync(Gender gender, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var yearCode = DateTime.UtcNow.ToString("yy");
            var genderCode = gender == Gender.Female ? "F" : "M";
            var prefix = $"SALU-{yearCode}-{genderCode}-";
            var lastRoll = await db.Enrollments.Where(enrollment => enrollment.RollNumber != null && enrollment.RollNumber.StartsWith(prefix))
                .OrderByDescending(enrollment => enrollment.RollNumber).Select(enrollment => enrollment.RollNumber).FirstOrDefaultAsync(cancellationToken);
            var next = lastRoll is not null && int.TryParse(lastRoll[prefix.Length..], out var current) ? current + 1 : 1;
            var rollNumber = $"{prefix}{next:D5}";
            await transaction.CommitAsync(cancellationToken);
            return rollNumber;
        });
    }
}

public sealed class EnrollmentService(IServiceScopeFactory scopeFactory) : IEnrollmentService
{
    public async Task<Enrollment> SaveDraftAsync(EnrollmentSubmission submission, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var existingDraft = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == submission.UserId && e.Status == EnrollmentStatus.Draft, cancellationToken);

        var existingSubmitted = await db.Enrollments
            .Where(e => e.UserId == submission.UserId && e.Status == EnrollmentStatus.Rejected)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var correctionAuthorized = existingSubmitted != null && await db.EnrollmentCorrectionRequests
            .AnyAsync(r => r.EnrollmentId == existingSubmitted.Id && r.Status == "Approved", cancellationToken);

        if (existingSubmitted != null && correctionAuthorized)
            existingDraft = existingSubmitted;
        else if (await db.Enrollments.AnyAsync(e => e.UserId == submission.UserId && e.Status != EnrollmentStatus.Draft, cancellationToken))
            throw new InvalidOperationException("This enrollment has already been submitted and cannot be modified.");

        if (existingDraft == null)
        {
            existingDraft = new Enrollment
            {
                UserId = submission.UserId,
                AcademicYearId = submission.AcademicYearId,
                Status = EnrollmentStatus.Draft,
                CreatedAt = now
            };
            db.Enrollments.Add(existingDraft);
        }

        existingDraft.AcademicYearId = submission.AcademicYearId;
        existingDraft.CollegeId = submission.CollegeId;
        existingDraft.Program = submission.Program?.Trim().ToUpperInvariant() ?? string.Empty;
        existingDraft.FatherName = submission.FatherName?.Trim().ToUpperInvariant() ?? string.Empty;
        existingDraft.Surname = submission.Surname?.Trim().ToUpperInvariant();
        existingDraft.SoDoWo = string.IsNullOrWhiteSpace(submission.SoDoWo) ? (submission.Gender == Gender.Female ? "D/O" : "S/O") : submission.SoDoWo.ToUpperInvariant();
        existingDraft.DateOfBirth = submission.DateOfBirth;
        existingDraft.Gender = submission.Gender;
        existingDraft.Address = submission.Address?.Trim().ToUpperInvariant() ?? string.Empty;
        existingDraft.ContactNumber = submission.ContactNumber?.Trim();
        existingDraft.DivisionObtained = submission.DivisionObtained?.Trim().ToUpperInvariant();
        existingDraft.PassingYear = submission.PassingYear;
        existingDraft.BoardName = submission.BoardName?.Trim().ToUpperInvariant();
        existingDraft.ExamFromSalu = submission.ExamFromSalu?.Trim().ToUpperInvariant();
        existingDraft.SaluSeatNo = submission.SaluSeatNo?.Trim().ToUpperInvariant();
        existingDraft.SaluExamYear = submission.SaluExamYear?.Trim().ToUpperInvariant();
        existingDraft.EligibilityCertNo = submission.EligibilityCertNo?.Trim().ToUpperInvariant();
        existingDraft.Nationality = string.IsNullOrWhiteSpace(submission.Nationality) ? "PAKISTANI" : submission.Nationality.Trim().ToUpperInvariant();
        existingDraft.Religion = string.IsNullOrWhiteSpace(submission.Religion) ? "ISLAM" : submission.Religion.Trim().ToUpperInvariant();
        existingDraft.DomicileProvince = string.IsNullOrWhiteSpace(submission.DomicileProvince) ? "SINDH" : submission.DomicileProvince.Trim().ToUpperInvariant();
        existingDraft.DomicileDistrict = submission.DomicileDistrict?.Trim().ToUpperInvariant() ?? string.Empty;
        existingDraft.MigrationProvince = string.IsNullOrWhiteSpace(submission.MigrationProvince) ? "SINDH" : submission.MigrationProvince.Trim().ToUpperInvariant();
        existingDraft.MigrationDistrict = submission.MigrationDistrict?.Trim().ToUpperInvariant();
        existingDraft.Session = submission.Session ?? $"{now.Year}-{now.Year + 1}";
        existingDraft.PhotoUrl = submission.PhotoUrl;
        existingDraft.AcademicRecordsJson = submission.AcademicRecordsJson;
        existingDraft.DocumentsJson = submission.DocumentsJson;
        existingDraft.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return existingDraft;
    }

    public async Task<Enrollment> SubmitAsync(EnrollmentSubmission submission, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var feeCalculationService = scope.ServiceProvider.GetRequiredService<IFeeCalculationService>();
        var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigurationService>();

        // 1. Check for duplicate already-submitted active application
        var hasActiveApplication = await db.Enrollments.AnyAsync(
            e => e.UserId == submission.UserId
                && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved
                    || (e.Status == EnrollmentStatus.Rejected
                        && !db.EnrollmentCorrectionRequests.Any(r => r.EnrollmentId == e.Id && r.Status == "Approved"))),
            cancellationToken);

        if (hasActiveApplication)
        {
            throw new InvalidOperationException("An active enrollment application already exists for this account. Multiple submissions are not permitted.");
        }

        // 2. Strict Server-side Validation
        if (submission.CollegeId is null || string.IsNullOrWhiteSpace(submission.Program))
            throw new InvalidOperationException("A valid affiliated college and degree program are required.");
        
        if (string.IsNullOrWhiteSpace(submission.FullName))
            throw new InvalidOperationException("Applicant full name is required.");
        
        if (string.IsNullOrWhiteSpace(submission.FatherName))
            throw new InvalidOperationException("Father's name is required.");

        var cleanCnic = Regex.Replace(submission.Cnic ?? string.Empty, @"\D", "");
        if (cleanCnic.Length != 13)
            throw new InvalidOperationException("CNIC must be a valid 13-digit Pakistani National ID number.");

        var cleanContact = Regex.Replace(submission.ContactNumber ?? string.Empty, @"\D", "");
        if (cleanContact.Length != 11 || !cleanContact.StartsWith("03"))
            throw new InvalidOperationException("Mobile number must be a valid 11-digit Pakistani mobile number (0300-0000000).");

        if (submission.DateOfBirth.Date >= DateTime.UtcNow.Date || submission.DateOfBirth.Date < DateTime.UtcNow.AddYears(-100).Date)
            throw new InvalidOperationException("Enter a valid date of birth.");

        if (!Enum.IsDefined(submission.Gender))
            throw new InvalidOperationException("Select a valid gender.");

        if (string.IsNullOrWhiteSpace(submission.Address))
            throw new InvalidOperationException("Permanent address is required.");

        if (string.IsNullOrWhiteSpace(submission.DomicileDistrict))
            throw new InvalidOperationException("Domicile district is required.");

        var validProgram = await db.CollegePrograms.AnyAsync(
            p => p.CollegeId == submission.CollegeId && (p.Code == submission.Program || p.Name == submission.Program) && p.IsActive,
            cancellationToken);

        if (!validProgram)
            throw new InvalidOperationException("The selected program is not offered or currently inactive at the selected college.");

        // 3. Automated Window Evaluation (Opening, Regular, Late, Closed)
        var windowStatus = await configService.GetEnrollmentWindowStatusAsync(cancellationToken);
        if (!windowStatus.IsActive)
        {
            throw new InvalidOperationException($"Enrollment cannot be submitted: {windowStatus.StatusDetailMessage}");
        }

        var now = DateTime.UtcNow;

        // 4. Atomic Database Transaction (Enrollment Record + Fee Challan) executed via Retriable Strategy
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                var fee = await feeCalculationService.CalculateAsync(submission.DomicileDistrict, cancellationToken);
                
                var randomRef = RandomNumberGenerator.GetInt32(100000, 999999);
                var refNumber = $"SALU-{now.Year}-{randomRef}";

                // Find existing draft or create new
                var enrollment = await db.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == submission.UserId && e.Status == EnrollmentStatus.Draft, cancellationToken);

                if (enrollment == null)
                {
                    enrollment = await db.Enrollments
                        .Where(e => e.UserId == submission.UserId && e.Status == EnrollmentStatus.Rejected
                            && db.EnrollmentCorrectionRequests.Any(r => r.EnrollmentId == e.Id && r.Status == "Approved"))
                        .OrderByDescending(e => e.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                if (enrollment == null)
                {
                    enrollment = new Enrollment
                    {
                        UserId = submission.UserId,
                        CreatedAt = now
                    };
                    db.Enrollments.Add(enrollment);
                }

                enrollment.AcademicYearId = submission.AcademicYearId;
                enrollment.CollegeId = submission.CollegeId;
                enrollment.Program = submission.Program.Trim().ToUpperInvariant();
                enrollment.FatherName = submission.FatherName.Trim().ToUpperInvariant();
                enrollment.Surname = submission.Surname?.Trim().ToUpperInvariant();
                enrollment.SoDoWo = string.IsNullOrWhiteSpace(submission.SoDoWo) ? (submission.Gender == Gender.Female ? "D/O" : "S/O") : submission.SoDoWo.ToUpperInvariant();
                enrollment.DateOfBirth = submission.DateOfBirth;
                enrollment.Gender = submission.Gender;
                enrollment.Address = submission.Address.Trim().ToUpperInvariant();
                enrollment.ContactNumber = submission.ContactNumber?.Trim();
                enrollment.DivisionObtained = submission.DivisionObtained?.Trim().ToUpperInvariant();
                enrollment.PassingYear = submission.PassingYear;
                enrollment.BoardName = submission.BoardName?.Trim().ToUpperInvariant();
                enrollment.ExamFromSalu = submission.ExamFromSalu?.Trim().ToUpperInvariant();
                enrollment.SaluSeatNo = submission.SaluSeatNo?.Trim().ToUpperInvariant();
                enrollment.SaluExamYear = submission.SaluExamYear?.Trim().ToUpperInvariant();
                enrollment.EligibilityCertNo = submission.EligibilityCertNo?.Trim().ToUpperInvariant();
                enrollment.Nationality = string.IsNullOrWhiteSpace(submission.Nationality) ? "PAKISTANI" : submission.Nationality.Trim().ToUpperInvariant();
                enrollment.Religion = string.IsNullOrWhiteSpace(submission.Religion) ? "ISLAM" : submission.Religion.Trim().ToUpperInvariant();
                enrollment.DomicileProvince = string.IsNullOrWhiteSpace(submission.DomicileProvince) ? "SINDH" : submission.DomicileProvince.Trim().ToUpperInvariant();
                enrollment.DomicileDistrict = submission.DomicileDistrict.Trim().ToUpperInvariant();
                enrollment.MigrationProvince = string.IsNullOrWhiteSpace(submission.MigrationProvince) ? "SINDH" : submission.MigrationProvince.Trim().ToUpperInvariant();
                enrollment.MigrationDistrict = submission.MigrationDistrict?.Trim().ToUpperInvariant();
                enrollment.Session = submission.Session ?? $"{now.Year}-{now.Year + 1}";
                enrollment.PhotoUrl = submission.PhotoUrl;
                enrollment.AcademicRecordsJson = submission.AcademicRecordsJson;
                enrollment.DocumentsJson = submission.DocumentsJson;
                enrollment.ReferenceNumber = refNumber;
                enrollment.Status = EnrollmentStatus.Pending;
                enrollment.UpdatedAt = now;

                await db.SaveChangesAsync(cancellationToken);

                // Fee Challan Processing
                var existingFee = await db.Fees.FirstOrDefaultAsync(f => f.EnrollmentId == enrollment.Id, cancellationToken);
                var challanNumber = $"SALU-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
                var feeNotes = $"Base: Rs. {fee.BaseFee:N0}";
                if (fee.LateFee > 0) feeNotes += $" + Late Surcharge: Rs. {fee.LateFee:N0}";
                if (fee.MigrationFee > 0) feeNotes += $" + Migration NOC: Rs. {fee.MigrationFee:N0}";

                if (existingFee != null)
                {
                    existingFee.Amount = fee.TotalFee;
                    existingFee.ChallanNumber = challanNumber;
                    existingFee.DueDate = now.AddDays(7);
                    existingFee.Notes = feeNotes;
                    existingFee.Status = FeeStatus.Unpaid;
                    existingFee.UpdatedAt = now;
                }
                else
                {
                    var newFee = new Fee
                    {
                        EnrollmentId = enrollment.Id,
                        Amount = fee.TotalFee,
                        ChallanNumber = challanNumber,
                        DueDate = now.AddDays(7),
                        Status = FeeStatus.Unpaid,
                        Notes = feeNotes,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    db.Fees.Add(newFee);
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return enrollment;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
