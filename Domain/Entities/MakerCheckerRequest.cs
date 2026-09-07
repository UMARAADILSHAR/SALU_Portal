namespace SaluExamPortal.Domain.Entities;

public sealed class MakerCheckerRequest : AuditableEntity
{
    public string RequestType { get; set; } = string.Empty; // e.g., 'StudentNameCorrection', 'GraceMarksApproval', 'DOBCorrection'
    public string TargetEntityName { get; set; } = string.Empty;
    public string TargetEntityId { get; set; } = string.Empty;
    public string ProposedPayloadJson { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string MakerUserId { get; set; } = string.Empty;
    public DateTime MakerSubmittedAt { get; set; } = DateTime.UtcNow;
    public string? CheckerUserId { get; set; }
    public DateTime? CheckerReviewedAt { get; set; }
    public string ReviewStatus { get; set; } = "Pending"; // 'Pending', 'Approved', 'Rejected'
    public string? RejectionReason { get; set; }
    public string? TotpVerificationRef { get; set; }
}
