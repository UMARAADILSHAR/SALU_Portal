namespace SaluExamPortal.Infrastructure.UniversityAdmission.Persistence;

public sealed class UniversityAdmissionApplication
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string FormStateJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
