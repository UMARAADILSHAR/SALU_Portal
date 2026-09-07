namespace SaluExamPortal.Domain.Entities;

public sealed class Seat : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public string ExamCenter { get; set; } = string.Empty;
    public string RoomNo { get; set; } = string.Empty;
    public string SeatNo { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; }
    public AdmitCard? AdmitCard { get; set; }
}
