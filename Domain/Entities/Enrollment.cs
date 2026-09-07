using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Domain.Entities;

public sealed class Enrollment : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public Guid? CollegeId { get; set; }
    public College? College { get; set; }
    public string Program { get; set; } = string.Empty;
    public string? Session { get; set; }
    public string Semester { get; set; } = "1";
    public string FatherName { get; set; } = string.Empty;
    public string? Surname { get; set; }
    public string? SoDoWo { get; set; } = "S/O";
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? ContactNumber { get; set; }
    public string? DivisionObtained { get; set; }
    public int? PassingYear { get; set; }
    public string? BoardName { get; set; }
    public string? ExamFromSalu { get; set; }
    public string? SaluSeatNo { get; set; }
    public string? SaluExamYear { get; set; }
    public string? EligibilityCertNo { get; set; }
    public string Nationality { get; set; } = "Pakistani";
    public string Religion { get; set; } = "Islam";
    public string DomicileProvince { get; set; } = "Sindh";
    public string DomicileDistrict { get; set; } = string.Empty;
    public string MigrationProvince { get; set; } = "Sindh";
    public string? MigrationDistrict { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? RollNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public string? AcademicRecordsJson { get; set; }
    public string? DocumentsJson { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Draft;
    public string? RejectionReason { get; set; }
    public ICollection<Fee> Fees { get; set; } = new List<Fee>();
    public Seat? Seat { get; set; }
    public AdmitCard? AdmitCard { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public ICollection<EnrollmentCorrectionRequest> CorrectionRequests { get; set; } = new List<EnrollmentCorrectionRequest>();
}
