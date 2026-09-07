namespace SaluExamPortal.Domain.Entities;

public sealed class Result : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int Marks { get; set; }
    public int TotalMarks { get; set; }
    public string Grade { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}
