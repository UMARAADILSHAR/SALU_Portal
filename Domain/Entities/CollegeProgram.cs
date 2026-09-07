namespace SaluExamPortal.Domain.Entities;

public sealed class CollegeProgram : AuditableEntity
{
    public Guid CollegeId { get; set; }
    public College College { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
