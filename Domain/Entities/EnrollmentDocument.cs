namespace SaluExamPortal.Domain.Entities;

public sealed class EnrollmentDocument : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ProcessingStatus { get; set; } = "Queued";
    public string? OcrResultJson { get; set; }
    public string? AuthenticityResultJson { get; set; }
    public string? LastError { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
