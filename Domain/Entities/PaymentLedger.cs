namespace SaluExamPortal.Domain.Entities;

/// <summary>
/// Append-only money trail. Rows must never be updated or deleted.
/// </summary>
public sealed class PaymentLedger
{
    public long Id { get; set; }
    public Guid FeeId { get; set; }
    public Fee Fee { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankTxnId { get; set; }
    public string? ScrollNo { get; set; }
    public string? BranchCode { get; set; }
    public DateTime? PaidAtBank { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? PayloadJson { get; set; }
    public string RowSha256 { get; set; } = string.Empty;
    public string? PrevRowSha256 { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
