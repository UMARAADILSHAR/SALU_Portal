using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Domain.Entities;

public sealed class Fee : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public string ChallanNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BaseFee { get; set; }
    public decimal LateFee { get; set; }
    public decimal MigrationFee { get; set; }
    public string Currency { get; set; } = "PKR";
    public string AmountInWordsEn { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountTitle { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string ContentSha256 { get; set; } = string.Empty;
    public Guid? PreviousFeeId { get; set; }
    public string? ReceiptRelativePath { get; set; }
    public string? ProposedPaidBy { get; set; }
    public DateTime? ProposedPaidAt { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public FeeStatus Status { get; set; } = FeeStatus.Unpaid;
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
    public string? Notes { get; set; }
    public ICollection<PaymentLedger> LedgerEntries { get; set; } = new List<PaymentLedger>();
}
