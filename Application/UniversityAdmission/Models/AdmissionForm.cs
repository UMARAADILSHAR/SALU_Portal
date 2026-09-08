using System.ComponentModel.DataAnnotations;

namespace SaluExamPortal.Application.UniversityAdmission.Models;

// ── Personal Info ────────────────────────────────────────────────
public class PersonalInfoModel
{
    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = "Zaffar Mutti";

    [Required(ErrorMessage = "Father's name is required.")]
    public string FatherName { get; set; } = "";

    public string Surname { get; set; } = "";

    [Required(ErrorMessage = "Gender is required.")]
    public string Gender { get; set; } = "";

    public string GuardianName { get; set; } = "";

    [Required(ErrorMessage = "CNIC is required.")]
    [RegularExpression(@"^\d{5}-\d{7}-\d{1}$", ErrorMessage = "CNIC format should be 12345-1234567-1.")]
    public string Cnic { get; set; } = "45203-1234567-1";

    [Required(ErrorMessage = "Date of birth is required.")]
    public DateTime? DateOfBirth { get; set; } = new DateTime(1998, 5, 14);

    [Required(ErrorMessage = "Current address is required.")]
    public string CurrentAddress { get; set; } = "";

    [Required(ErrorMessage = "Permanent address is required.")]
    public string PermanentAddress { get; set; } = "";

    public string CorrespondenceAddress { get; set; } = "";

    [Required(ErrorMessage = "Nationality is required.")]
    public string Nationality { get; set; } = "Pakistani";

    [Required(ErrorMessage = "Religion is required.")]
    public string Religion { get; set; } = "";

    [Required(ErrorMessage = "Domicile province is required.")]
    public string DomicileProvince { get; set; } = "Sindh";

    [Required(ErrorMessage = "Domicile district is required.")]
    public string DomicileDistrict { get; set; } = "";

    public string Tehsil { get; set; } = "";
    public string ResidenceNumber { get; set; } = "";

    [Required(ErrorMessage = "Mobile number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    public string MobileNo { get; set; } = "0300-1234567";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = "zaffarmutti@gmail.com";

    public bool IsEmployed { get; set; } = false;
    public bool SelfFinance { get; set; } = false;
    public bool FirstFamilyMember { get; set; } = false;
    public bool AvailHostel { get; set; } = false;
    public bool AvailTransport { get; set; } = false;
}

// ── Academics ────────────────────────────────────────────────────
public class AcademicRecord
{
    [Required(ErrorMessage = "Examination is required.")]
    public string Examination { get; set; } = "";

    [Required(ErrorMessage = "Group / major is required.")]
    public string GroupMajor { get; set; } = "";

    [Required(ErrorMessage = "Year is required.")]
    public string Year { get; set; } = "";

    [Required(ErrorMessage = "Seat number is required.")]
    public string SeatNo { get; set; } = "";

    [Required(ErrorMessage = "Obtained marks are required.")]
    public string ObtainedMarks { get; set; } = "";

    [Required(ErrorMessage = "Total marks are required.")]
    public string TotalMarks { get; set; } = "";

    [Required(ErrorMessage = "Board is required.")]
    public string Board { get; set; } = "";

    [Required(ErrorMessage = "Institute is required.")]
    public string Institute { get; set; } = "";
}

public class AcademicsModel
{
    public string Faculty { get; set; } = "";
    public string Department { get; set; } = "";
    public string Program { get; set; } = "";
    public string QualifyingDegree { get; set; } = "";
    public string EligibleMajor { get; set; } = "";
    public string AcademicYear { get; set; } = "";
    public List<AcademicRecord> History { get; set; } = [];
}

// ── Preference ───────────────────────────────────────────────────
public class PreferenceRecord
{
    public string Faculty { get; set; } = "";
    public string Department { get; set; } = "";
    public string Program { get; set; } = "";
}

// ── Document ─────────────────────────────────────────────────────
public class DocumentRecord
{
    public string DocType { get; set; } = "";
    public string DocName { get; set; } = "";
    public string FileName { get; set; } = "";
}

// ── Form State ──────────────────────────────────────────────────
public class AdmissionFormState
{
    public PersonalInfoModel Personal { get; set; } = new();
    public AcademicsModel Academics { get; set; } = new();
    public List<PreferenceRecord> Preferences { get; set; } = [];
    public List<DocumentRecord> Documents { get; set; } = [];
    public bool UndertakingAccepted { get; set; } = false;

    public bool PersonalDone { get; set; } = false;
    public bool AcademicsDone { get; set; } = false;
    public bool PreferencesDone { get; set; } = false;
    public bool DocumentsDone { get; set; } = false;
    public bool UndertakingDone { get; set; } = false;

    public bool AllMandatoryComplete =>
        PersonalDone && AcademicsDone && DocumentsDone && UndertakingDone;
}

