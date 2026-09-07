using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Application.Services;

public sealed class ExamIntegrityReconciliationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExamIntegrityReconciliationWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public ExamIntegrityReconciliationWorker(
        IServiceProvider serviceProvider,
        ILogger<ExamIntegrityReconciliationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExamIntegrityReconciliationWorker started. Anti-Skip audit active.");

        // Initial brief delay before starting periodic reconciliation cycle
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformReconciliationAuditAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error occurred during Exam Integrity Reconciliation cycle.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task PerformReconciliationAuditAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        _logger.LogInformation("Running ledger reconciliation audit: verified fees vs issued admit cards...");

        var activeAcademicYears = await dbContext.AcademicYears
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var year in activeAcademicYears)
        {
            var eligibleEnrollmentIds = (await dbContext.Enrollments
                .Where(e => e.AcademicYearId == year.Id
                            && e.Status == EnrollmentStatus.Approved
                            && e.Fees.Any(f => f.Status == FeeStatus.Verified))
                .Select(e => e.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var issuedEnrollmentIds = (await dbContext.AdmitCards
                .Where(a => a.Enrollment.AcademicYearId == year.Id && a.IsIssued)
                .Select(a => a.EnrollmentId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var missingIds = eligibleEnrollmentIds.Except(issuedEnrollmentIds).ToList();
            var phantomIds = issuedEnrollmentIds.Except(eligibleEnrollmentIds).ToList();

            if (missingIds.Count > 0)
            {
                _logger.LogCritical(
                    "[ANTI-SKIP SECURITY ALERT]: Academic Year '{YearName}' has {MissingCount} eligible enrollment(s) without admit cards. Enrollment IDs: {EnrollmentIds}",
                    year.Name, missingIds.Count, string.Join(',', missingIds));
            }

            if (phantomIds.Count > 0)
            {
                _logger.LogCritical(
                    "[FRAUD DETECTED]: Academic Year '{YearName}' contains {PhantomCount} admit card(s) without Approved enrollment and Verified fee. Enrollment IDs: {EnrollmentIds}",
                    year.Name, phantomIds.Count, string.Join(',', phantomIds));
            }

            if (missingIds.Count == 0 && phantomIds.Count == 0)
            {
                _logger.LogInformation("Academic Year '{YearName}' is reconciled for {Total} eligible enrollment(s).", year.Name, eligibleEnrollmentIds.Count);
            }
        }
    }
}
