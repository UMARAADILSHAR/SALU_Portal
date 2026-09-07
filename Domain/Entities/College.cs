using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Domain.Entities;

public sealed class College : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string Province { get; set; } = "Sindh";
    public string District { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PrincipalName { get; set; }
    public CollegeType Type { get; set; }
    public int BoysCapacity { get; set; } = 500;
    public int GirlsCapacity { get; set; } = 500;
    public bool IsActive { get; set; } = true;
    public ICollection<CollegeProgram> Programs { get; set; } = new List<CollegeProgram>();
}
