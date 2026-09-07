namespace SaluExamPortal.Domain.Entities;

public sealed class BankReconciliationBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public int MatchedCount { get; set; }
    public int ExceptionCount { get; set; }
    public string? Notes { get; set; }
    public ICollection<BankReconciliationLine> Lines { get; set; } = new List<BankReconciliationLine>();
}

public sealed class BankReconciliationLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public BankReconciliationBatch Batch { get; set; } = null!;
    public string ChallanNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankTxnId { get; set; }
    public DateTime? PaidAtBank { get; set; }
    public string? BranchCode { get; set; }
    public string Channel { get; set; } = "HBL";
    public string MatchStatus { get; set; } = "Exception";
    public Guid? FeeId { get; set; }
    public Fee? Fee { get; set; }
    public string? Message { get; set; }
}
