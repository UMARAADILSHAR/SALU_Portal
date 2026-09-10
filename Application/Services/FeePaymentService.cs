using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QRCoder;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Constants;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public sealed record BankScrollImportResult(int Matched, int Exceptions, Guid BatchId);

public interface IFeePaymentService
{
    Task<Fee> IssueChallanForEnrollmentAsync(Guid enrollmentId, string domicileDistrict, string actorUserId, CancellationToken cancellationToken = default);
    Task<Fee?> GetActiveChallanAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task ExpireOverdueChallansAsync(CancellationToken cancellationToken = default);
    Task<Fee> IssueReplacementChallanAsync(Guid enrollmentId, string actorUserId, CancellationToken cancellationToken = default);
    Task UploadStudentReceiptAsync(Guid feeId, Stream fileStream, string fileName, string actorUserId, CancellationToken cancellationToken = default);
    Task ProposePaidAsync(Guid feeId, string actorUserId, string bankTxnId, decimal amount, string channel, DateTime paidAtBank, string? branchCode, string? totpCode, CancellationToken cancellationToken = default);
    Task VerifyPaidAsync(Guid feeId, string checkerUserId, string? totpCode, CancellationToken cancellationToken = default);
    Task RecordUniversityCopyReceivedAsync(Guid feeId, string actorUserId, CancellationToken cancellationToken = default);
    Task<BankScrollImportResult> ImportBankScrollAsync(Stream csvStream, string fileName, string actorUserId, CancellationToken cancellationToken = default);
    byte[] GenerateChallanQrPng(Fee fee);
    string BuildQrPayload(Fee fee);
}

public sealed class FeePaymentService(
    IApplicationDbContext db,
    IFeeCalculationService feeCalculationService,
    ISystemConfigurationService configService,
    IFileStorageService fileStorage,
    ITotpService totpService,
    IExamAdmitCardEngineService admitCardEngine,
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FeePaymentService> logger) : IFeePaymentService
{
    public async Task<Fee> IssueChallanForEnrollmentAsync(Guid enrollmentId, string domicileDistrict, string actorUserId, CancellationToken cancellationToken = default)
    {
        await ExpireOverdueForEnrollmentAsync(enrollmentId, cancellationToken);

        var existing = await GetReusableChallanAsync(enrollmentId, cancellationToken);
        if (existing != null)
            return existing;

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException("Enrollment was not found.");

        var breakdown = await feeCalculationService.CalculateAsync(domicileDistrict, cancellationToken);
        var settings = await configService.GetEnrollmentSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var validityDays = Math.Clamp(settings.ChallanValidityDays, 1, 90);
        var seq = await NextChallanSequenceAsync(cancellationToken);
        var challanNumber = $"SALU-{now:yyyy}-{seq:D8}";

        var fee = new Fee
        {
            EnrollmentId = enrollment.Id,
            ChallanNumber = challanNumber,
            BaseFee = breakdown.BaseFee,
            LateFee = breakdown.LateFee,
            MigrationFee = breakdown.MigrationFee,
            Amount = breakdown.TotalFee,
            Currency = "PKR",
            AmountInWordsEn = PkrAmountInWords.ToEnglish(breakdown.TotalFee),
            IssuedAtUtc = now,
            DueDate = now.AddDays(validityDays),
            Status = FeeStatus.Unpaid,
            BankName = settings.CollectionBankName,
            AccountTitle = settings.CollectionAccountTitle,
            AccountNumber = settings.CollectionAccountNumber,
            Iban = settings.CollectionIban,
            Notes = BuildNotes(breakdown),
            CreatedAt = now,
            UpdatedAt = now
        };
        fee.ContentSha256 = ComputeChallanHash(fee);

        var previous = await db.Fees
            .Where(f => f.EnrollmentId == enrollmentId && f.Status == FeeStatus.Expired)
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (previous != null)
            fee.PreviousFeeId = previous.Id;

        db.Fees.Add(fee);
        await db.SaveChangesAsync(cancellationToken);

        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.Issued,
            fee.Amount,
            PaymentChannels.System,
            $"issued:{fee.Id:N}",
            actorUserId,
            payloadJson: null,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Issued immutable challan {Challan} for enrollment {EnrollmentId} amount {Amount}", challanNumber, enrollmentId, fee.Amount);
        return fee;
    }

    public async Task<Fee?> GetActiveChallanAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        await ExpireOverdueForEnrollmentAsync(enrollmentId, cancellationToken);
        return await db.Fees
            .Where(f => f.EnrollmentId == enrollmentId && (
                f.Status == FeeStatus.Unpaid
                || f.Status == FeeStatus.PendingVerification
                || f.Status == FeeStatus.Paid
                || f.Status == FeeStatus.Verified
                || f.Status == FeeStatus.Expired))
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ExpireOverdueChallansAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await db.Fees
            .Where(f => (f.Status == FeeStatus.Unpaid || f.Status == FeeStatus.PendingVerification)
                        && f.DueDate < now)
            .ToListAsync(cancellationToken);

        foreach (var fee in overdue)
            await ExpireFeeAsync(fee, "System", cancellationToken);

        if (overdue.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Fee> IssueReplacementChallanAsync(Guid enrollmentId, string actorUserId, CancellationToken cancellationToken = default)
    {
        await ExpireOverdueForEnrollmentAsync(enrollmentId, cancellationToken);

        var latest = await db.Fees
            .Where(f => f.EnrollmentId == enrollmentId)
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No challan exists for this enrollment.");

        if (FeeStatusRules.IsActiveIssue(latest.Status) && latest.Status != FeeStatus.Expired)
            throw new InvalidOperationException("An active challan already exists. Expired challans only can be replaced.");

        if (latest.Status is FeeStatus.Paid or FeeStatus.Verified)
            throw new InvalidOperationException("Paid or verified challans cannot be replaced.");

        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);
        if (latest.Status == FeeStatus.Expired)
        {
            latest.Status = FeeStatus.Superseded;
            await AppendLedgerAsync(latest, PaymentEventTypes.Superseded, 0, PaymentChannels.System, $"supersede:{latest.Id:N}:{DateTime.UtcNow.Ticks}", actorUserId, null, cancellationToken);
        }

        return await IssueChallanForEnrollmentAsync(enrollmentId, enrollment.DomicileDistrict, actorUserId, cancellationToken);
    }

    public async Task UploadStudentReceiptAsync(Guid feeId, Stream fileStream, string fileName, string actorUserId, CancellationToken cancellationToken = default)
    {
        var fee = await db.Fees
            .Include(f => f.Enrollment)
            .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
            ?? throw new InvalidOperationException("Challan not found.");

        if (!FeeStatusRules.CanUploadReceipt(fee.Status))
            throw new InvalidOperationException("A receipt can only be uploaded for an unpaid or pending challan.");

        var stored = await fileStorage.SaveDocumentAsync(fileStream, fileName, "FeeReceipt", cancellationToken);
        fee.ReceiptRelativePath = stored.RelativePath;
        fee.Status = FeeStatus.PendingVerification;
        fee.PaymentMethod = PaymentChannels.StudentReceipt;

        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.StudentReceiptUploaded,
            0,
            PaymentChannels.StudentReceipt,
            $"receipt:{fee.Id:N}:{stored.Id}",
            actorUserId,
            payloadJson: stored.FileName,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ProposePaidAsync(
        Guid feeId,
        string actorUserId,
        string bankTxnId,
        decimal amount,
        string channel,
        DateTime paidAtBank,
        string? branchCode,
        string? totpCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bankTxnId) || bankTxnId.Trim().Length < 4)
            throw new InvalidOperationException("A bank transaction / scroll number of at least 4 characters is required.");

        var fee = await db.Fees.FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
            ?? throw new InvalidOperationException("Challan not found.");

        if (!FeeStatusRules.CanProposePaid(fee.Status))
            throw new InvalidOperationException($"Challan {fee.ChallanNumber} cannot be marked bank-posted from status {fee.Status}.");

        if (amount != fee.Amount)
            throw new InvalidOperationException($"Amount PKR {amount:N2} does not match issued challan PKR {fee.Amount:N2}.");

        await EnsureOfficerAsync(actorUserId, totpCode, requireTotpIfEnabled: false, cancellationToken);

        var txn = bankTxnId.Trim().ToUpperInvariant();
        var key = $"bank:{channel}:{txn}";
        if (await db.PaymentLedgers.AnyAsync(l => l.IdempotencyKey == key, cancellationToken))
            throw new InvalidOperationException("This bank transaction id has already been posted to the ledger.");

        fee.Status = FeeStatus.Paid;
        fee.PaidAt = DateTime.UtcNow;
        fee.TransactionId = txn;
        fee.PaymentMethod = channel;
        fee.ProposedPaidBy = actorUserId;
        fee.ProposedPaidAt = DateTime.UtcNow;

        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.BankPosted,
            fee.Amount,
            string.IsNullOrWhiteSpace(channel) ? PaymentChannels.Manual : channel,
            key,
            actorUserId,
            bankTxnId: txn,
            scrollNo: txn,
            branchCode: branchCode,
            paidAtBank: paidAtBank.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(paidAtBank, DateTimeKind.Utc) : paidAtBank.ToUniversalTime(),
            payloadJson: null,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Maker {User} posted bank payment for challan {Challan} txn {Txn}", actorUserId, fee.ChallanNumber, txn);
    }

    public async Task VerifyPaidAsync(Guid feeId, string checkerUserId, string? totpCode, CancellationToken cancellationToken = default)
    {
        var fee = await db.Fees
            .Include(f => f.Enrollment)
            .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
            ?? throw new InvalidOperationException("Challan not found.");

        if (!FeeStatusRules.CanVerify(fee.Status))
            throw new InvalidOperationException("Only bank-posted (Paid) challans can be verified by a checker.");

        if (string.Equals(fee.ProposedPaidBy, checkerUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("The checker must be a different officer than the maker who posted the bank payment.");

        await EnsureOfficerAsync(checkerUserId, totpCode, requireTotpIfEnabled: true, cancellationToken);

        fee.Status = FeeStatus.Verified;
        fee.VerifiedBy = checkerUserId;
        fee.VerifiedAt = DateTime.UtcNow;

        // A verified challan is the financial gate for the examination workflow.
        // Promote a submitted enrollment before invoking the serializable seat allocator.
        if (fee.Enrollment.Status == EnrollmentStatus.Pending)
        {
            fee.Enrollment.Status = EnrollmentStatus.Approved;
            fee.Enrollment.RejectionReason = null;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = checkerUserId,
                Action = "APPROVE_ENROLLMENT_ON_FEE_VERIFICATION",
                Entity = nameof(Enrollment),
                EntityId = fee.Enrollment.Id.ToString(),
                Details = $"Enrollment approved after verified challan {fee.ChallanNumber}."
            });
        }

        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.Verified,
            fee.Amount,
            PaymentChannels.Manual,
            $"verified:{fee.Id:N}",
            checkerUserId,
            payloadJson: null,
            cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = checkerUserId,
            Action = "VERIFY_FEE",
            Entity = nameof(Fee),
            EntityId = fee.Id.ToString(),
            Details = $"Verified challan {fee.ChallanNumber} amount {fee.Amount:N2}"
        });

        await db.SaveChangesAsync(cancellationToken);

        // Allocation is idempotent and protected by a serializable transaction;
        // running it here removes the fragile manual hand-off to Exam Hub.
        await admitCardEngine.ProcessPaidStudentEnrollmentsAsync(fee.Enrollment.AcademicYearId, cancellationToken);
        logger.LogInformation("Checker {User} verified challan {Challan}", checkerUserId, fee.ChallanNumber);
    }

    public async Task RecordUniversityCopyReceivedAsync(Guid feeId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var fee = await db.Fees.FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
            ?? throw new InvalidOperationException("Challan not found.");

        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.UniversityCopyReceived,
            0,
            PaymentChannels.Manual,
            $"unicopy:{fee.Id:N}:{DateTime.UtcNow.Ticks}",
            actorUserId,
            payloadJson: null,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BankScrollImportResult> ImportBankScrollAsync(Stream csvStream, string fileName, string actorUserId, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new InvalidOperationException("Bank scroll CSV must include a header row and at least one data row. Columns: ChallanNumber,Amount,BankTxnId,PaidAt,BranchCode,Channel");

        var batch = new BankReconciliationBatch
        {
            FileName = Path.GetFileName(fileName),
            UploadedBy = actorUserId,
            UploadedAtUtc = DateTime.UtcNow
        };
        db.BankReconciliationBatches.Add(batch);

        var matched = 0;
        var exceptions = 0;
        foreach (var raw in lines.Skip(1))
        {
            var cols = SplitCsv(raw);
            var line = new BankReconciliationLine
            {
                Batch = batch,
                ChallanNumber = cols.ElementAtOrDefault(0)?.Trim() ?? "",
                Amount = decimal.TryParse(cols.ElementAtOrDefault(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) ? amt : 0,
                BankTxnId = cols.ElementAtOrDefault(2)?.Trim(),
                PaidAtBank = DateTime.TryParse(cols.ElementAtOrDefault(3), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var paid) ? paid : null,
                BranchCode = cols.ElementAtOrDefault(4)?.Trim(),
                Channel = string.IsNullOrWhiteSpace(cols.ElementAtOrDefault(5)) ? PaymentChannels.BankScroll : cols[5].Trim()
            };

            try
            {
                var fee = await db.Fees.FirstOrDefaultAsync(f => f.ChallanNumber == line.ChallanNumber, cancellationToken);
                if (fee == null)
                    throw new InvalidOperationException("Unknown challan number.");
                if (line.Amount != fee.Amount)
                    throw new InvalidOperationException($"Amount mismatch: scroll {line.Amount:N2} vs challan {fee.Amount:N2}.");
                if (string.IsNullOrWhiteSpace(line.BankTxnId))
                    throw new InvalidOperationException("BankTxnId is required.");

                if (FeeStatusRules.CanProposePaid(fee.Status))
                {
                    await ProposePaidAsync(
                        fee.Id,
                        actorUserId,
                        line.BankTxnId,
                        line.Amount,
                        line.Channel,
                        line.PaidAtBank ?? DateTime.UtcNow,
                        line.BranchCode,
                        totpCode: null,
                        cancellationToken);
                }
                else if (fee.Status is FeeStatus.Paid or FeeStatus.Verified)
                {
                    // idempotent success
                }
                else
                {
                    throw new InvalidOperationException($"Challan status {fee.Status} cannot be matched.");
                }

                line.FeeId = fee.Id;
                line.MatchStatus = "Matched";
                line.Message = "Exact challan number and amount matched.";
                matched++;
            }
            catch (Exception ex)
            {
                line.MatchStatus = "Exception";
                line.Message = ex.Message;
                exceptions++;
            }

            batch.Lines.Add(line);
        }

        batch.MatchedCount = matched;
        batch.ExceptionCount = exceptions;
        await db.SaveChangesAsync(cancellationToken);
        return new BankScrollImportResult(matched, exceptions, batch.Id);
    }

    public byte[] GenerateChallanQrPng(Fee fee)
    {
        var payload = BuildQrPayload(fee);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(6);
    }

    public string BuildQrPayload(Fee fee) =>
        $"CHALLAN|{fee.ChallanNumber}|{fee.Amount:F2}|{fee.DueDate:yyyy-MM-dd}|{fee.ContentSha256}";

    private async Task EnsureOfficerAsync(string userId, string? totpCode, bool requireTotpIfEnabled, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("Officer account was not found.");
        if (!user.IsVerified)
            throw new UnauthorizedAccessException("Officer account is not verified.");

        if (requireTotpIfEnabled && user.TotpEnabled && !string.IsNullOrWhiteSpace(user.TotpSecretEncrypted))
        {
            if (string.IsNullOrWhiteSpace(totpCode))
                throw new InvalidOperationException("A TOTP code is required to verify fee payments.");
            var secret = totpService.DecryptTotpSecret(user.TotpSecretEncrypted);
            if (!totpService.ValidateTotpCode(secret, totpCode))
                throw new UnauthorizedAccessException("Invalid or expired TOTP code.");
        }
        _ = cancellationToken;
    }

    private async Task<Fee?> GetReusableChallanAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        return await db.Fees
            .Where(f => f.EnrollmentId == enrollmentId && (
                f.Status == FeeStatus.Unpaid
                || f.Status == FeeStatus.PendingVerification
                || f.Status == FeeStatus.Paid
                || f.Status == FeeStatus.Verified))
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task ExpireOverdueForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overdue = await db.Fees
            .Where(f => f.EnrollmentId == enrollmentId
                        && (f.Status == FeeStatus.Unpaid || f.Status == FeeStatus.PendingVerification)
                        && f.DueDate < now)
            .ToListAsync(cancellationToken);

        foreach (var fee in overdue)
            await ExpireFeeAsync(fee, "System", cancellationToken);

        if (overdue.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireFeeAsync(Fee fee, string actorUserId, CancellationToken cancellationToken)
    {
        fee.Status = FeeStatus.Expired;
        await AppendLedgerAsync(
            fee,
            PaymentEventTypes.Expired,
            0,
            PaymentChannels.System,
            $"expired:{fee.Id:N}",
            actorUserId,
            payloadJson: null,
            cancellationToken);
    }

    private async Task AppendLedgerAsync(
        Fee fee,
        string eventType,
        decimal amount,
        string channel,
        string idempotencyKey,
        string actorUserId,
        string? payloadJson,
        CancellationToken cancellationToken,
        string? bankTxnId = null,
        string? scrollNo = null,
        string? branchCode = null,
        DateTime? paidAtBank = null)
    {
        var lastHash = await db.PaymentLedgers
            .OrderByDescending(l => l.Id)
            .Select(l => l.RowSha256)
            .FirstOrDefaultAsync(cancellationToken);

        var created = DateTime.UtcNow;
        var http = httpContextAccessor.HttpContext;
        var row = new PaymentLedger
        {
            FeeId = fee.Id,
            EventType = eventType,
            Amount = amount,
            BankTxnId = bankTxnId,
            ScrollNo = scrollNo,
            BranchCode = branchCode,
            PaidAtBank = paidAtBank,
            Channel = channel,
            IdempotencyKey = idempotencyKey,
            ActorUserId = actorUserId,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http?.Request.Headers.UserAgent.ToString(),
            PayloadJson = payloadJson,
            PrevRowSha256 = lastHash,
            CreatedAtUtc = created
        };
        row.RowSha256 = ComputeLedgerHash(row);
        db.PaymentLedgers.Add(row);
    }

    private async Task<long> NextChallanSequenceAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR dbo.FeeChallanSeq";
        var currentTx = db.Database.CurrentTransaction;
        if (currentTx != null)
            command.Transaction = currentTx.GetDbTransaction();

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    public static string ComputeChallanHash(Fee fee)
    {
        var canonical =
            $"{fee.ChallanNumber}|{fee.Amount:F2}|{fee.BaseFee:F2}|{fee.LateFee:F2}|{fee.MigrationFee:F2}|{fee.DueDate:O}|{fee.IssuedAtUtc:O}|{fee.AccountNumber}|{fee.Iban}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string ComputeLedgerHash(PaymentLedger row)
    {
        var canonical =
            $"{row.PrevRowSha256}|{row.FeeId:N}|{row.EventType}|{row.Amount:F2}|{row.BankTxnId}|{row.IdempotencyKey}|{row.CreatedAtUtc:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string BuildNotes(FeeBreakdown breakdown)
    {
        var notes = $"Base: Rs. {breakdown.BaseFee:N0}";
        if (breakdown.LateFee > 0) notes += $" + Late: Rs. {breakdown.LateFee:N0}";
        if (breakdown.MigrationFee > 0) notes += $" + Migration NOC: Rs. {breakdown.MigrationFee:N0}";
        return notes;
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        result.Add(current.ToString());
        return result;
    }
}
