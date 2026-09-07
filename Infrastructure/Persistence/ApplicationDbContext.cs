using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Infrastructure.Persistence
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
    {
        public DbSet<College> Colleges => Set<College>();
        public DbSet<CollegeProgram> CollegePrograms => Set<CollegeProgram>();
        public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
        public DbSet<EnrollmentWindow> EnrollmentWindows => Set<EnrollmentWindow>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<EnrollmentCorrectionRequest> EnrollmentCorrectionRequests => Set<EnrollmentCorrectionRequest>();
        public DbSet<EnrollmentDocument> EnrollmentDocuments => Set<EnrollmentDocument>();
        public DbSet<Fee> Fees => Set<Fee>();
        public DbSet<Seat> Seats => Set<Seat>();
        public DbSet<AdmitCard> AdmitCards => Set<AdmitCard>();
        public DbSet<Result> Results => Set<Result>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<MakerCheckerRequest> MakerCheckerRequests => Set<MakerCheckerRequest>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<PaymentLedger> PaymentLedgers => Set<PaymentLedger>();
        public DbSet<BankReconciliationBatch> BankReconciliationBatches => Set<BankReconciliationBatch>();
        public DbSet<BankReconciliationLine> BankReconciliationLines => Set<BankReconciliationLine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(user => user.FullName).HasMaxLength(255).IsRequired();
                entity.Property(user => user.FatherName).HasMaxLength(255);
                entity.Property(user => user.Cnic).HasMaxLength(15);
                entity.HasIndex(user => user.Cnic).IsUnique().HasFilter("[Cnic] IS NOT NULL");
                entity.HasOne(user => user.College)
                    .WithMany()
                    .HasForeignKey(user => user.CollegeId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                // TOTP 2FA configuration
                entity.Property(user => user.TotpSecretEncrypted).HasMaxLength(500);
                entity.Property(user => user.TotpEnabled).HasDefaultValue(false);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.Property(log => log.Action).HasMaxLength(100).IsRequired();
                entity.Property(log => log.Entity).HasMaxLength(100).IsRequired();
                entity.HasIndex(log => new { log.Entity, log.EntityId, log.CreatedAt });
            });

            modelBuilder.Entity<College>(entity =>
            {
                entity.Property(college => college.Name).HasMaxLength(255).IsRequired();
                entity.Property(college => college.Code).HasMaxLength(50).IsRequired();
                entity.Property(college => college.District).HasMaxLength(100).IsRequired();
                entity.Property(college => college.Type).HasConversion<string>().HasMaxLength(20);
                entity.HasIndex(college => college.Code).IsUnique();
            });

            modelBuilder.Entity<CollegeProgram>(entity =>
            {
                entity.Property(program => program.Name).HasMaxLength(100).IsRequired();
                entity.Property(program => program.Code).HasMaxLength(20).IsRequired();
                entity.HasIndex(program => new { program.CollegeId, program.Code }).IsUnique();
                entity.HasOne(program => program.College)
                    .WithMany(college => college.Programs)
                    .HasForeignKey(program => program.CollegeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EnrollmentDocument>(entity =>
            {
                entity.Property(document => document.FileId).HasMaxLength(100).IsRequired();
                entity.Property(document => document.DocumentType).HasMaxLength(200).IsRequired();
                entity.Property(document => document.ProcessingStatus).HasMaxLength(30).IsRequired();
                entity.HasIndex(document => document.FileId).IsUnique();
                entity.HasIndex(document => new { document.ProcessingStatus, document.CreatedAt });
                entity.HasOne(document => document.Enrollment)
                    .WithMany()
                    .HasForeignKey(document => document.EnrollmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EnrollmentCorrectionRequest>(entity =>
            {
                entity.Property(request => request.Status).HasMaxLength(20).IsRequired();
                entity.Property(request => request.RequestedBy).HasMaxLength(450).IsRequired();
                entity.Property(request => request.ReviewedBy).HasMaxLength(450);
                entity.HasIndex(request => new { request.EnrollmentId, request.Status });
                entity.HasOne(request => request.Enrollment)
                    .WithMany(enrollment => enrollment.CorrectionRequests)
                    .HasForeignKey(request => request.EnrollmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AcademicYear>(entity =>
            {
                entity.Property(year => year.Name).HasMaxLength(50).IsRequired();
                entity.HasIndex(year => year.Name).IsUnique();
            });

            modelBuilder.Entity<EnrollmentWindow>(entity =>
            {
                entity.HasOne(window => window.AcademicYear)
                    .WithMany(year => year.EnrollmentWindows)
                    .HasForeignKey(window => window.AcademicYearId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Enrollment>(entity =>
            {
                entity.Property(enrollment => enrollment.Program).HasMaxLength(100).IsRequired();
                entity.Property(enrollment => enrollment.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(enrollment => enrollment.Gender).HasConversion<string>().HasMaxLength(10);
                entity.HasIndex(enrollment => enrollment.RollNumber).IsUnique().HasFilter("[RollNumber] IS NOT NULL");
                entity.HasIndex(enrollment => enrollment.ReferenceNumber).IsUnique().HasFilter("[ReferenceNumber] IS NOT NULL");
                entity.HasOne(enrollment => enrollment.AcademicYear)
                    .WithMany(year => year.Enrollments)
                    .HasForeignKey(enrollment => enrollment.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(enrollment => enrollment.College)
                    .WithMany()
                    .HasForeignKey(enrollment => enrollment.CollegeId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<ApplicationUser>()
                    .WithMany(user => user.Enrollments)
                    .HasForeignKey(enrollment => enrollment.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Fee>(entity =>
            {
                entity.Property(fee => fee.Status).HasConversion<string>().HasMaxLength(25);
                entity.Property(fee => fee.Amount).HasPrecision(10, 2);
                entity.Property(fee => fee.BaseFee).HasPrecision(18, 2);
                entity.Property(fee => fee.LateFee).HasPrecision(18, 2);
                entity.Property(fee => fee.MigrationFee).HasPrecision(18, 2);
                entity.HasIndex(fee => fee.ChallanNumber).IsUnique();
                entity.HasOne(fee => fee.Enrollment).WithMany(enrollment => enrollment.Fees)
                    .HasForeignKey(fee => fee.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(fee => new { fee.Status, fee.DueDate });
                entity.HasIndex(fee => fee.TransactionId);
            });

            modelBuilder.Entity<Seat>(entity =>
            {
                entity.HasIndex(seat => seat.EnrollmentId).IsUnique();
                entity.HasOne(seat => seat.Enrollment).WithOne(enrollment => enrollment.Seat)
                    .HasForeignKey<Seat>(seat => seat.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AdmitCard>(entity =>
            {
                entity.Property(card => card.RollNumber).HasMaxLength(50).IsRequired();
                entity.Property(card => card.QRVerificationHash).HasMaxLength(64);
                entity.HasIndex(card => card.EnrollmentId).IsUnique();
                entity.HasIndex(card => card.RollNumber).IsUnique().HasFilter("[RollNumber] IS NOT NULL AND [RollNumber] <> ''");
                entity.HasOne(card => card.Enrollment).WithOne(enrollment => enrollment.AdmitCard)
                    .HasForeignKey<AdmitCard>(card => card.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(card => card.Seat).WithOne(seat => seat.AdmitCard)
                    .HasForeignKey<AdmitCard>(card => card.SeatId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<MakerCheckerRequest>(entity =>
            {
                entity.Property(req => req.RequestType).HasMaxLength(50).IsRequired();
                entity.Property(req => req.TargetEntityName).HasMaxLength(100).IsRequired();
                entity.Property(req => req.TargetEntityId).HasMaxLength(100).IsRequired();
                entity.Property(req => req.MakerUserId).HasMaxLength(100).IsRequired();
                entity.Property(req => req.ReviewStatus).HasMaxLength(20).IsRequired();
                entity.HasIndex(req => req.ReviewStatus);
            });

            modelBuilder.Entity<SystemSetting>().HasKey(setting => setting.Key);
            modelBuilder.Entity<SystemSetting>().Property(setting => setting.Key).HasMaxLength(100);

            modelBuilder.Entity<PaymentLedger>(entity =>
            {
                entity.Property(ledger => ledger.Amount).HasPrecision(18, 2);
                entity.Property(ledger => ledger.EventType).HasMaxLength(50).IsRequired();
                entity.Property(ledger => ledger.Channel).HasMaxLength(50).IsRequired();
                entity.Property(ledger => ledger.IdempotencyKey).HasMaxLength(200).IsRequired();
                entity.Property(ledger => ledger.ActorUserId).HasMaxLength(450).IsRequired();
                entity.HasIndex(ledger => ledger.IdempotencyKey).IsUnique();
                entity.HasIndex(ledger => new { ledger.FeeId, ledger.CreatedAtUtc });
                entity.HasOne(ledger => ledger.Fee).WithMany(fee => fee.LedgerEntries)
                    .HasForeignKey(ledger => ledger.FeeId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BankReconciliationBatch>(entity =>
            {
                entity.Property(batch => batch.FileName).HasMaxLength(255).IsRequired();
                entity.Property(batch => batch.UploadedBy).HasMaxLength(450).IsRequired();
                entity.HasMany(batch => batch.Lines).WithOne(line => line.Batch)
                    .HasForeignKey(line => line.BatchId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BankReconciliationLine>(entity =>
            {
                entity.Property(line => line.Amount).HasPrecision(18, 2);
                entity.Property(line => line.ChallanNumber).HasMaxLength(100).IsRequired();
                entity.HasIndex(line => new { line.BatchId, line.ChallanNumber });
                entity.HasOne(line => line.Fee).WithMany()
                    .HasForeignKey(line => line.FeeId).OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
