using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Application.Services;

public sealed record AdmitCardBatchResult(int ProcessedCount, int SkippedCount, IReadOnlyList<string> AssignedRollNumbers);

public interface IExamAdmitCardEngineService
{
    Task<AdmitCardBatchResult> ProcessPaidStudentEnrollmentsAsync(Guid academicYearId, CancellationToken cancellationToken = default);
}

public sealed class ExamAdmitCardEngineService : IExamAdmitCardEngineService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<ExamAdmitCardEngineService> _logger;
    private readonly byte[] _hmacSecretKey;

    public ExamAdmitCardEngineService(
        IApplicationDbContext dbContext,
        ILogger<ExamAdmitCardEngineService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        var secret = configuration["Security:AdmitCardHmacSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Security:AdmitCardHmacSecret must be configured; a fallback secret is not permitted.");
        _hmacSecretKey = Encoding.UTF8.GetBytes(secret);
    }

    public async Task<AdmitCardBatchResult> ProcessPaidStudentEnrollmentsAsync(Guid academicYearId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ACID Seat Allocation for Academic Year {AcademicYearId}", academicYearId);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Execute inside strict Serializable isolation to avoid race conditions and guarantee gapless roll numbers
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                // 1. Identify all verified enrollments with Paid fee that DO NOT yet have an AdmitCard
                var unassignedPaidEnrollments = await _dbContext.Enrollments
                    .Include(e => e.Fees)
                    .Include(e => e.College)
                    .Where(e => e.AcademicYearId == academicYearId
                             && e.Status == EnrollmentStatus.Approved
                             && e.Fees.Any(f => f.Status == FeeStatus.Verified)
                             && !_dbContext.AdmitCards.Any(card => card.EnrollmentId == e.Id))
                    .OrderBy(e => e.Program)
                    .ThenBy(e => e.CreatedAt)
                    .ToListAsync(cancellationToken);

                if (unassignedPaidEnrollments.Count == 0)
                {
                    _logger.LogInformation("No pending paid student enrollments requiring admit card generation for year {AcademicYearId}.", academicYearId);
                    return new AdmitCardBatchResult(0, 0, Array.Empty<string>());
                }

                // 2. Determine base sequence from existing roll numbers in this academic year
                var yearCode = DateTime.UtcNow.ToString("yy");
                var prefix = $"SALU-{yearCode}-";

                var lastRollNumber = await _dbContext.AdmitCards
                    .Where(c => c.RollNumber.StartsWith(prefix))
                    .OrderByDescending(c => c.RollNumber)
                    .Select(c => c.RollNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                int currentSequence = 0;
                if (!string.IsNullOrWhiteSpace(lastRollNumber))
                {
                    var parts = lastRollNumber.Split('-');
                    if (parts.Length >= 3 && int.TryParse(parts[^1], out var parsedSeq))
                    {
                        currentSequence = parsedSeq;
                    }
                }

                var generatedRollNumbers = new List<string>(unassignedPaidEnrollments.Count);
                var nowUtc = DateTime.UtcNow;
                int seatIndex = 1;
                int roomIndex = 1;

                foreach (var enrollment in unassignedPaidEnrollments)
                {
                    currentSequence++;
                    var genderCode = enrollment.Gender == Gender.Female ? "F" : "M";
                    var rollNumber = $"{prefix}{genderCode}-{currentSequence:D5}";

                    var centerCode = "CTR-SALU-MAIN";
                    var centerName = enrollment.College?.Name ?? "Shah Abdul Latif University Main Campus, Khairpur";
                    var roomNo = $"Hall-{roomIndex:D2}";
                    var examDate = nowUtc.AddDays(14); // 2 weeks out standard examination date

                    // 3. Generate HMAC-SHA256 Digital Verification Hash
                    var qrPayload = $"{enrollment.Id}|{rollNumber}|{centerCode}|{enrollment.ReferenceNumber}|{nowUtc:O}";
                    var qrHash = ComputeHmacSha256(qrPayload, _hmacSecretKey);

                    // Update enrollment with final roll number and approved status
                    enrollment.RollNumber = rollNumber;
                    enrollment.Status = EnrollmentStatus.Approved;

                    var admitCard = new AdmitCard
                    {
                        EnrollmentId = enrollment.Id,
                        RollNumber = rollNumber,
                        CenterCode = centerCode,
                        ExamCenter = centerName,
                        RoomNumber = roomNo,
                        SeatNumber = seatIndex++,
                        ExamDate = examDate,
                        IsIssued = true,
                        IssuedAt = nowUtc,
                        QRVerificationHash = qrHash,
                        GeneratedAtUtc = nowUtc
                    };

                    _dbContext.AdmitCards.Add(admitCard);
                    generatedRollNumbers.Add(rollNumber);

                    if (seatIndex > 40)
                    {
                        seatIndex = 1;
                        roomIndex++;
                    }
                }

                // 4. Commit all records atomically
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("ACID Seat Allocation succeeded: Generated {Count} gapless admit cards for Academic Year {AcademicYearId}.",
                    generatedRollNumbers.Count, academicYearId);

                return new AdmitCardBatchResult(generatedRollNumbers.Count, 0, generatedRollNumbers);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "FATAL: ACID Seat Allocation transaction failed for Academic Year {AcademicYearId}. All changes rolled back.", academicYearId);
                throw;
            }
        });
    }

    private static string ComputeHmacSha256(string rawData, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
