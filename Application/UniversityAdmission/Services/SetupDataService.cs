using System.Text.Json;
using SaluExamPortal.Application.UniversityAdmission.Models;

namespace SaluExamPortal.Application.UniversityAdmission.Services;

public class SetupDataService
{
    private readonly IWebHostEnvironment _env;
    private SetupDataRoot? _data;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SetupDataService(IWebHostEnvironment env)
    {
        _env = env;
    }

    private async Task<SetupDataRoot> LoadAsync()
    {
        if (_data != null) return _data;
        await _lock.WaitAsync();
        try
        {
            if (_data != null) return _data;
            var path = Path.Combine(_env.WebRootPath, "university-admission", "salu_pg_setup_data.json");
            await using var fs = File.OpenRead(path);
            _data = await JsonSerializer.DeserializeAsync<SetupDataRoot>(fs)
                    ?? new SetupDataRoot();
        }
        finally { _lock.Release(); }
        return _data;
    }

    public async Task<List<string>> GetAcademicYearsAsync()
        => (await LoadAsync()).AdmissionSetup?.AcademicYears ?? [];

    public async Task<List<string>> GetYearsForPGAsync()
        => (await LoadAsync()).AdmissionSetup?.YearsForPG ?? [];

    public async Task<List<IdNameItem>> GetMeritListsAsync()
        => (await LoadAsync()).AdmissionSetup?.MeritLists ?? [];

    public async Task<List<DocumentType>> GetDocumentTypesAsync()
        => (await LoadAsync()).AdmissionSetup?.DocumentTypes ?? [];

    public async Task<List<ChecklistItem>> GetChecklistItemsAsync()
        => (await LoadAsync()).AdmissionSetup?.ChecklistItems ?? [];

    public async Task<List<IdNameItem>> GetFeeParticularsAsync()
        => (await LoadAsync()).AdmissionSetup?.FeeParticulars ?? [];

    public static readonly List<string> DefaultNationalities = ["Pakistani", "Overseas Pakistani", "Foreign National"];
    public static readonly List<string> DefaultProvinces = ["Sindh", "Punjab", "KPK", "Balochistan", "Islamabad", "Gilgit Baltistan", "Azad Jammu & Kashmir"];

    public static readonly List<DistrictTehsilItem> SindhDistricts =
    [
        new(5584, "Badin", ["Badin", "Matli", "Shaheed Fazil Rahu", "Talhar", "Tando Bago"]),
        new(5585, "Dadu", ["Dadu", "Johi", "Khairpur Nathan Shah", "Mehar"]),
        new(5586, "Ghotki", ["Daharki", "Ghotki", "Khan Garh (Khanpur)", "Mirpur Mathelo", "Ubauro"]),
        new(5587, "Hyderabad", ["Hyderabad City", "Hyderabad", "Latifabad", "Qasimabad"]),
        new(5588, "Jacobabad", ["Garhi Khairo", "Jacobabad", "Thul"]),
        new(5589, "Jamshoro", ["Kotri", "Manjhand", "Sehwan", "Thana Bulla Khan"]),
        new(5590, "Karachi Central", ["Gulberg", "Liaquatabad", "North Nazimabad"]),
        new(5591, "Karachi East", ["Gulshan Town", "Jamshed Town"]),
        new(5593, "Karachi West", ["Baldia Town", "Keamari Town", "Orangi Town"]),
        new(5594, "Kashmore", ["Bakrani", "Kandhkot", "Kashmore", "Tangwani"]),
        new(5596, "Khairpur", ["Faiz Ganj", "Gambat", "Khairpur", "Kingri", "Kot Diji", "Mirwah", "Nara", "Sobho Dero"]),
        new(5598, "Larkana", ["Dokri", "Larkana", "Ratodero"]),
        new(5601, "Matiari", ["Hala", "Matiari", "Saeedabad"]),
        new(5602, "Mirpur Khas", ["Digri", "Hussain Bux Mari", "Jhuddo", "Kot Ghulam Muhammad", "Mirpur Khas", "Shujabad", "Sindhri"]),
        new(5603, "Naushahro Feroze", ["Bhiria", "Kandiaro", "Mehrabpur", "Moro", "Naushahro Feroze"]),
        new(5604, "Qambar Shahdadkot", ["Kambar", "Miro Khan", "Nasirabad", "Qambar", "Qubo Saeed Khan", "Shahdadkot", "Sijawal Junejo", "Warah"]),
        new(5605, "Sanghar", ["Jam Nawaz Ali", "Khipro", "Sanghar", "Shahdadpur", "Sinjhoro", "Tando Adam Khan"]),
        new(5606, "Shaheed Benazirabad", ["Daur", "Kazi Ahmed", "Nawabshah", "Sakrand"]),
        new(5607, "Shikarpur", ["Garhi Yasin", "Khanpur", "Lakhi", "Shikarpur"]),
        new(5608, "Sujawal", ["Jati", "Kharo Chan", "Mirpur Bathoro", "Shah Bunder"]),
        new(5609, "Sukkur", ["New Sukkur", "Pano Akil", "Rohri", "Salehpat", "Sukkur"])
    ];

    public async Task<List<string>> GetNationalitiesAsync()
    {
        var list = (await LoadAsync()).AdmissionSetup?.Nationalities;
        return list != null && list.Count > 0 ? list : DefaultNationalities;
    }

    public async Task<List<string>> GetProvincesAsync()
    {
        var list = (await LoadAsync()).AdmissionSetup?.Provinces;
        return list != null && list.Count > 0 ? list : DefaultProvinces;
    }

    public async Task<List<IdNameItem>> GetExaminationsAsync()
        => (await LoadAsync()).AdmissionSetup?.Examinations ?? [];

    public async Task<List<IdNameItem>> GetBoardsAsync()
        => (await LoadAsync()).AdmissionSetup?.Boards ?? [];

    public async Task<List<string>> GetLastDegreesAsync()
        => (await LoadAsync()).AdmissionSetup?.LastDegrees ?? [];

    public async Task<List<string>> GetEligibleMajorsAsync()
        => (await LoadAsync()).AdmissionSetup?.EligibleMajors ?? [];

    // ── Eligibility Criteria database (static, mirrors JS criteriaDatabase) ──
    public static readonly Dictionary<string, List<ProgramCriteria>> CriteriaDatabase = new()
    {
        ["BS-I / BBA-I"] =
        [
            new() { Prog = "BS Computer Science (BS-I)", Dept = "Computer Science", Faculty = "Physical Sciences", Req = "HSSC / Intermediate in Pre-Engineering / General Science (with Mathematics) / ICS", Min = "Min 50% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS Information Technology (BS-I)", Dept = "Information Technology", Faculty = "Physical Sciences", Req = "HSSC / Intermediate in Pre-Engineering / General Science / ICS", Min = "Min 50% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BBA-I (Bachelor of Business Administration)", Dept = "Business Administration", Faculty = "Management Sciences", Req = "HSSC / Intermediate in Pre-Engineering, Pre-Medical, Commerce, or Arts", Min = "Min 50% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS Chemistry (BS-I)", Dept = "Chemistry", Faculty = "Natural Sciences", Req = "HSSC Pre-Medical / Pre-Engineering with Chemistry", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS Microbiology (BS-I)", Dept = "Microbiology", Faculty = "Natural Sciences", Req = "HSSC Pre-Medical with Biology & Chemistry", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS Zoology (BS-I)", Dept = "Zoology", Faculty = "Natural Sciences", Req = "HSSC Pre-Medical with Biology", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS Botany (BS-I)", Dept = "Botany", Faculty = "Natural Sciences", Req = "HSSC Pre-Medical with Biology", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "BS English Literature & Linguistics (BS-I)", Dept = "English", Faculty = "Arts & Languages", Req = "HSSC Intermediate in any group (Pre-Eng / Pre-Med / Humanities / Commerce)", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
            new() { Prog = "B.A LL.B (5 Years)", Dept = "Law", Faculty = "Law", Req = "HSSC Intermediate in any discipline with LAT (Law Admission Test)", Min = "Min 50% Marks + 50% in LAT", Test = "HEC LAT Mandatory" },
            new() { Prog = "BS Commerce (BS-I)", Dept = "Commerce", Faculty = "Commerce", Req = "HSSC / I.Com / D.Com / Intermediate in any group", Min = "Min 45% Marks", Test = "SALU Pre-Admission Test" },
        ],
        ["BS-III"] =
        [
            new() { Prog = "BS Computer Science (BS-III Lateral)", Dept = "Computer Science", Faculty = "Physical Sciences", Req = "ADS / B.Sc with Computer Science / Mathematics / Physics (14 Years)", Min = "Min 50% Marks or CGPA 2.5", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS Chemistry (BS-III Lateral)", Dept = "Chemistry", Faculty = "Natural Sciences", Req = "B.Sc (Part-II) in Chemistry with Zoology, Botany, or Mathematics", Min = "Min 45% Marks or CGPA 2.2", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS Microbiology (BS-III Lateral)", Dept = "Microbiology", Faculty = "Natural Sciences", Req = "B.Sc (Part-II) in Microbiology with Chemistry / Biochemistry", Min = "Min 45% Marks or CGPA 2.2", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS Zoology (BS-III Lateral)", Dept = "Zoology", Faculty = "Natural Sciences", Req = "B.Sc (Part-II) in Zoology with Chemistry / Botany", Min = "Min 45% Marks", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS Botany (BS-III Lateral)", Dept = "Botany", Faculty = "Natural Sciences", Req = "B.Sc (Part-II) in Botany with Chemistry / Zoology", Min = "Min 45% Marks", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS English (BS-III Lateral)", Dept = "English", Faculty = "Arts & Languages", Req = "B.A (Pass) 2 years with English Compulsory / Elective", Min = "Min 45% Marks", Test = "Departmental Scrutiny & Interview" },
            new() { Prog = "BS Commerce (BS-III Lateral)", Dept = "Commerce", Faculty = "Commerce", Req = "B.Com (Pass) 2 years or ADC (Associate Degree in Commerce)", Min = "Min 45% Marks", Test = "Departmental Scrutiny & Interview" },
        ],
        ["MBA 1.5"] =
        [
            new() { Prog = "MBA (1.5 Years)", Dept = "Business Administration", Faculty = "Management Sciences", Req = "16 years Business education (BBA 4-Year / B.Com 4-Year / M.Com)", Min = "Min CGPA 2.5/4.0 or 50% Marks", Test = "GAT (General) or SALU Entry Test" },
        ],
        ["MBA 3.5"] =
        [
            new() { Prog = "MBA (3.5 Years)", Dept = "Business Administration", Faculty = "Management Sciences", Req = "14 years non-business education (B.A / B.Sc / B.Com 2-Year)", Min = "Min 45% Marks", Test = "GAT (General) or SALU Entry Test" },
        ],
        ["MS / M.Phil"] =
        [
            new() { Prog = "MS / M.Phil Computer Science", Dept = "Computer Science", Faculty = "Physical Sciences", Req = "16 Years BS Computer Science / IT / Software Engineering (130+ Cr. Hrs)", Min = "Min CGPA 2.5/4.0 or 50% Marks", Test = "GAT-General (Min 50%) or SALU PG Test" },
            new() { Prog = "MS / M.Phil Information Technology", Dept = "Information Technology", Faculty = "Physical Sciences", Req = "16 Years BS IT / BS CS / Software Eng", Min = "Min CGPA 2.5/4.0 or 50% Marks", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Chemistry", Dept = "Chemistry", Faculty = "Natural Sciences", Req = "16 Years BS Chemistry 4-Year or M.Sc Chemistry", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Microbiology", Dept = "Microbiology", Faculty = "Natural Sciences", Req = "16 Years BS Microbiology / Biochemistry / Biotechnology", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Zoology", Dept = "Zoology", Faculty = "Natural Sciences", Req = "16 Years BS Zoology 4-Year or M.Sc Zoology", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Botany", Dept = "Botany", Faculty = "Natural Sciences", Req = "16 Years BS Botany 4-Year or M.Sc Botany", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil English (Literature & Linguistics)", Dept = "English", Faculty = "Arts & Languages", Req = "16 Years BS English or MA English", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Sindhi", Dept = "Sindhi", Faculty = "Arts & Languages", Req = "16 Years BS Sindhi or MA Sindhi", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "M.Phil Archaeology", Dept = "Archaeology", Faculty = "Arts & Languages", Req = "16 Years BS Archaeology / History / Anthropology", Min = "Min 50% Marks or CGPA 2.5", Test = "GAT-General or SALU PG Test" },
            new() { Prog = "MS / M.Phil Commerce", Dept = "Commerce", Faculty = "Commerce", Req = "16 Years B.Com 4-Year / BS Commerce / M.Com", Min = "Min CGPA 2.5/4.0 or 50% Marks", Test = "GAT-General or SALU PG Test" },
        ],
        ["Ph.D"] =
        [
            new() { Prog = "Ph.D Computer Science", Dept = "Computer Science", Faculty = "Physical Sciences", Req = "18 Years MS / M.Phil in Computer Science / Software Engineering", Min = "Min CGPA 3.0/4.0 or 60% Marks", Test = "GAT Subject / HEC Test (Min 60%)" },
            new() { Prog = "Ph.D Chemistry", Dept = "Chemistry", Faculty = "Natural Sciences", Req = "18 Years MS / M.Phil in Chemistry", Min = "Min CGPA 3.0/4.0 or 60% Marks", Test = "GAT Subject / HEC Test (Min 60%)" },
            new() { Prog = "Ph.D Microbiology", Dept = "Microbiology", Faculty = "Natural Sciences", Req = "18 Years MS / M.Phil in Microbiology", Min = "Min CGPA 3.0/4.0 or 60% Marks", Test = "GAT Subject / HEC Test (Min 60%)" },
            new() { Prog = "Ph.D Zoology", Dept = "Zoology", Faculty = "Natural Sciences", Req = "18 Years MS / M.Phil in Zoology", Min = "Min CGPA 3.0/4.0 or 60% Marks", Test = "GAT Subject / HEC Test (Min 60%)" },
            new() { Prog = "Ph.D Botany", Dept = "Botany", Faculty = "Natural Sciences", Req = "18 Years MS / M.Phil in Botany", Min = "Min CGPA 3.0/4.0 or 60% Marks", Test = "GAT Subject / HEC Test (Min 60%)" },
        ],
    };

    public static List<string> Faculties => [.. FacultyDepartmentsMap.Keys];

    // ── Faculty → Departments map ─────────────────────────────────
    public static readonly Dictionary<string, List<string>> FacultyDepartmentsMap = new()
    {
        ["Physical Sciences"] = ["Computer Science", "Information Technology", "Mathematics", "Physics", "Statistics"],
        ["Natural Sciences"] = ["Chemistry", "Microbiology", "Zoology", "Botany", "Biochemistry"],
        ["Arts & Languages"] = ["Arabic", "Archaeology", "English", "Sindhi", "Urdu"],
        ["Management Sciences"] = ["Business Administration", "Public Administration"],
        ["Commerce"] = ["Commerce"],
        ["Law"] = ["Law"],
        ["Education"] = ["Teacher Education"],
    };

    // ── Department → Programs map ─────────────────────────────────
    public static readonly Dictionary<string, List<ProgramEntry>> DepartmentProgramsMap = new()
    {
        ["Computer Science"] =
        [
            new() { Name = "BS Computer Science (BS-I)", Deg = "HSSC (Pre-Eng / ICS / Gen. Science)", Majors = ["Computer Science", "Mathematics"] },
            new() { Name = "BS Computer Science (BS-III Lateral)", Deg = "ADS / B.Sc (2 Years)", Majors = ["Computer Science"] },
            new() { Name = "MS / M.Phil Computer Science", Deg = "BS CS / BS IT (4 Years - 130+ Cr. Hrs)", Majors = ["Computer Science", "Information Technology"] },
            new() { Name = "Ph.D Computer Science", Deg = "MS / M.Phil Computer Science (18 Years)", Majors = ["Computer Science"] },
        ],
        ["Information Technology"] =
        [
            new() { Name = "BS Information Technology (BS-I)", Deg = "HSSC (Pre-Eng / ICS / Gen. Science)", Majors = ["Information Technology", "Computer Science"] },
            new() { Name = "MS / M.Phil Information Technology", Deg = "BS IT / BS CS (4 Years)", Majors = ["Information Technology", "Computer Science"] },
        ],
        ["Chemistry"] =
        [
            new() { Name = "BS Chemistry (BS-I)", Deg = "HSSC Pre-Medical / Pre-Engineering", Majors = ["Chemistry"] },
            new() { Name = "BS Chemistry (BS-III Lateral)", Deg = "B.Sc (Part-II) in Chemistry", Majors = ["Chemistry with Physics", "Chemistry with Zoology"] },
            new() { Name = "M.Phil Chemistry", Deg = "BS Chemistry 4-Year / M.Sc Chemistry", Majors = ["Chemistry"] },
            new() { Name = "Ph.D Chemistry", Deg = "MS / M.Phil in Chemistry", Majors = ["Chemistry"] },
        ],
        ["Microbiology"] =
        [
            new() { Name = "BS Microbiology (BS-I)", Deg = "HSSC Pre-Medical", Majors = ["Microbiology", "Biology"] },
            new() { Name = "BS Microbiology (BS-III Lateral)", Deg = "B.Sc (Part-II) in Microbiology", Majors = ["Microbiology with Chemistry", "Microbiology with Zoology"] },
            new() { Name = "M.Phil Microbiology", Deg = "BS Microbiology 4-Year", Majors = ["Microbiology", "Biochemistry"] },
            new() { Name = "Ph.D Microbiology", Deg = "MS / M.Phil in Microbiology", Majors = ["Microbiology"] },
        ],
        ["Zoology"] =
        [
            new() { Name = "BS Zoology (BS-I)", Deg = "HSSC Pre-Medical", Majors = ["Zoology", "Biology"] },
            new() { Name = "BS Zoology (BS-III Lateral)", Deg = "B.Sc (Part-II) in Zoology", Majors = ["Zoology with Botany", "Zoology with Chemistry"] },
            new() { Name = "M.Phil Zoology", Deg = "BS Zoology 4-Year", Majors = ["Zoology"] },
            new() { Name = "Ph.D Zoology", Deg = "MS / M.Phil in Zoology", Majors = ["Zoology"] },
        ],
        ["Botany"] =
        [
            new() { Name = "BS Botany (BS-I)", Deg = "HSSC Pre-Medical", Majors = ["Botany", "Biology"] },
            new() { Name = "BS Botany (BS-III Lateral)", Deg = "B.Sc (Part-II) in Botany", Majors = ["Botany with Chemistry", "Botany with Zoology"] },
            new() { Name = "M.Phil Botany", Deg = "BS Botany 4-Year", Majors = ["Botany"] },
            new() { Name = "Ph.D Botany", Deg = "MS / M.Phil in Botany", Majors = ["Botany"] },
        ],
        ["English"] =
        [
            new() { Name = "BS English (BS-I)", Deg = "HSSC Intermediate any group", Majors = ["Arts", "English"] },
            new() { Name = "BS English (BS-III Lateral)", Deg = "B.A (Pass) 2 years with English", Majors = ["Arts"] },
            new() { Name = "M.Phil English", Deg = "BS English 4-Year or MA English", Majors = ["English"] },
            new() { Name = "Ph.D English", Deg = "MS / M.Phil English", Majors = ["English"] },
        ],
        ["Business Administration"] =
        [
            new() { Name = "BBA-I (4 Years)", Deg = "HSSC Intermediate any group", Majors = ["Arts", "Commerce", "Science"] },
            new() { Name = "MBA 1.5", Deg = "16 years Business education (BBA/B.Com)", Majors = ["16 years Business education"] },
            new() { Name = "MBA 3.5", Deg = "14 years graduation (BA/B.Sc/B.Com)", Majors = ["Arts", "Commerce", "Science"] },
            new() { Name = "MS / M.Phil Management Sciences", Deg = "BBA 4-Year / MBA (16 Years)", Majors = ["16 years Business education"] },
        ],
        ["Commerce"] =
        [
            new() { Name = "BS Commerce (BS-I)", Deg = "HSSC / I.Com / Intermediate", Majors = ["Commerce"] },
            new() { Name = "BS Commerce (BS-III Lateral)", Deg = "B.Com (Pass) 2 years / ADC", Majors = ["B.Com (Part-II)"] },
            new() { Name = "MS / M.Phil Commerce", Deg = "B.Com 4-Year / M.Com (16 Years)", Majors = ["Commerce"] },
        ],
        ["Law"] =
        [
            new() { Name = "B.A LL.B (5 Years)", Deg = "HSSC Intermediate with LAT", Majors = ["Arts", "Science", "Commerce"] },
        ],
        ["Mathematics"] =
        [
            new() { Name = "BS Mathematics (BS-I)", Deg = "HSSC Pre-Engineering", Majors = ["Pure Mathematics", "Functional Mathematics"] },
            new() { Name = "MS / M.Phil Mathematics", Deg = "BS Mathematics 4-Year", Majors = ["Mathematics"] },
        ],
        ["Physics"] =
        [
            new() { Name = "BS Physics (BS-I)", Deg = "HSSC Pre-Engineering", Majors = ["Physics"] },
            new() { Name = "MS / M.Phil Physics", Deg = "BS Physics 4-Year", Majors = ["Physics"] },
        ],
        ["Sindhi"] =
        [
            new() { Name = "BS Sindhi (BS-I)", Deg = "HSSC Intermediate", Majors = ["Arts", "Sindhi"] },
            new() { Name = "M.Phil Sindhi", Deg = "BS Sindhi 4-Year or MA Sindhi", Majors = ["Sindhi"] },
        ],
        ["Archaeology"] =
        [
            new() { Name = "BS Archaeology (BS-I)", Deg = "HSSC Intermediate", Majors = ["Archaeology", "Arts"] },
            new() { Name = "M.Phil Archaeology", Deg = "BS Archaeology 4-Year", Majors = ["Archaeology"] },
        ],
        ["Teacher Education"] =
        [
            new() { Name = "B.Ed(Hons) Elementary (4 Years)", Deg = "HSSC Intermediate", Majors = ["Arts", "Science"] },
            new() { Name = "B.Ed (1.5 Years)", Deg = "16 Years Master's degree (MA/M.Sc)", Majors = ["Arts", "Science"] },
            new() { Name = "B.Ed (2.5 Years)", Deg = "14 Years Bachelor's degree (BA/B.Sc)", Majors = ["Arts", "Science"] },
        ],
    };
}

