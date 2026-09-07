namespace SaluExamPortal.Domain.Entities;

public sealed class AdmitCard : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public Guid? SeatId { get; set; }
    public Seat? Seat { get; set; }
    public string RollNumber { get; set; } = string.Empty;
    public string CenterCode { get; set; } = string.Empty;
    public string ExamCenter { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public DateTime ExamDate { get; set; }
    public bool IsIssued { get; set; } = true;
    public DateTime? IssuedAt { get; set; }
    public string QRVerificationHash { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
