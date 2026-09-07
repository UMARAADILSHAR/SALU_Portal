namespace SaluExamPortal.Domain.Entities;

public sealed class EnrollmentWindow : AuditableEntity
{
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsOpen { get; set; } = true;
}
