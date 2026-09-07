namespace SaluExamPortal.Domain.Entities;

public sealed class EnrollmentCorrectionRequest : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public string AllowedFieldsJson { get; set; } = "[]";
    public string Justification { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
