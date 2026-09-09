using System.Text.Json.Serialization;

namespace SaluExamPortal.Application.UniversityAdmission.Models;

public class SetupDataRoot
{
    [JsonPropertyName("metadata")]
    public SetupMetadata? Metadata { get; set; }

    [JsonPropertyName("admissionSetup")]
    public AdmissionSetup? AdmissionSetup { get; set; }
}

public class SetupMetadata
{
    [JsonPropertyName("institution")]
    public string Institution { get; set; } = "";

    [JsonPropertyName("system")]
    public string System { get; set; } = "";

    [JsonPropertyName("totalRawRecords")]
    public int TotalRawRecords { get; set; }

    [JsonPropertyName("totalMasterCategories")]
    public int TotalMasterCategories { get; set; }

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";
}

public class AdmissionSetup
{
    [JsonPropertyName("academicYears")]
    public List<string> AcademicYears { get; set; } = [];

    [JsonPropertyName("yearsForPG")]
    public List<string> YearsForPG { get; set; } = [];

    [JsonPropertyName("meritLists")]
    public List<IdNameItem> MeritLists { get; set; } = [];

    [JsonPropertyName("documentTypes")]
    public List<DocumentType> DocumentTypes { get; set; } = [];

    [JsonPropertyName("checklistItems")]
    public List<ChecklistItem> ChecklistItems { get; set; } = [];

    [JsonPropertyName("feeParticulars")]
    public List<IdNameItem> FeeParticulars { get; set; } = [];

    [JsonPropertyName("nationalities")]
    public List<string> Nationalities { get; set; } = [];

    [JsonPropertyName("provinces")]
    public List<string> Provinces { get; set; } = [];

    [JsonPropertyName("examinations")]
    public List<IdNameItem> Examinations { get; set; } = [];

    [JsonPropertyName("boards")]
    public List<IdNameItem> Boards { get; set; } = [];

    [JsonPropertyName("lastDegrees")]
    public List<string> LastDegrees { get; set; } = [];

    [JsonPropertyName("eligibleMajors")]
    public List<string> EligibleMajors { get; set; } = [];
}

public class IdNameItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("order")]
    public string? Order { get; set; }
}

public class DocumentType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("master")]
    public string Master { get; set; } = "";
}

public class ChecklistItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parentId")]
    public int ParentId { get; set; }
}

public class DistrictTehsilItem
{
    [JsonPropertyName("districtDetailId")]
    public int DistrictDetailId { get; set; }

    [JsonPropertyName("district")]
    public string District { get; set; } = "";

    [JsonPropertyName("tehsils")]
    public List<string> Tehsils { get; set; } = [];

    public DistrictTehsilItem() { }

    public DistrictTehsilItem(int id, string district, IEnumerable<string> tehsils)
    {
        DistrictDetailId = id;
        District = district;
        Tehsils = [.. tehsils];
    }
}

