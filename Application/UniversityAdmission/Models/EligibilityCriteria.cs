namespace SaluExamPortal.Application.UniversityAdmission.Models;

public class ProgramCriteria
{
    public string Prog { get; set; } = "";
    public string Dept { get; set; } = "";
    public string Faculty { get; set; } = "";
    public string Req { get; set; } = "";
    public string Min { get; set; } = "";
    public string Test { get; set; } = "";
}

public class ProgramEntry
{
    public string Name { get; set; } = "";
    public string Deg { get; set; } = "";
    public List<string> Majors { get; set; } = [];
}

