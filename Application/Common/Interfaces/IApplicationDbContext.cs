using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Domain.Entities;

namespace SaluExamPortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<College> Colleges { get; }
    DbSet<CollegeProgram> CollegePrograms { get; }
    DbSet<AcademicYear> AcademicYears { get; }
    DbSet<EnrollmentWindow> EnrollmentWindows { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<EnrollmentCorrectionRequest> EnrollmentCorrectionRequests { get; }
    DbSet<EnrollmentDocument> EnrollmentDocuments { get; }
    DbSet<Fee> Fees { get; }
    DbSet<Seat> Seats { get; }
    DbSet<AdmitCard> AdmitCards { get; }
    DbSet<Result> Results { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<MakerCheckerRequest> MakerCheckerRequests { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<PaymentLedger> PaymentLedgers { get; }
    DbSet<BankReconciliationBatch> BankReconciliationBatches { get; }
    DbSet<BankReconciliationLine> BankReconciliationLines { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
