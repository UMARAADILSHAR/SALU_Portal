using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Domain.Entities;

public sealed class AcademicYear : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public AcademicYearStatus Status { get; set; } = AcademicYearStatus.Active;
    public ICollection<EnrollmentWindow> EnrollmentWindows { get; set; } = new List<EnrollmentWindow>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
