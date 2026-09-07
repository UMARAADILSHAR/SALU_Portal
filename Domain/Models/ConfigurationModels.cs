using System;

namespace SaluExamPortal.Domain.Models;

public sealed class GeneralSettings
{
    public string UniversityName { get; set; } = "Shah Abdul Latif University, Khairpur";
    public string SystemName { get; set; } = "Shah Abdul Latif University Exam Portal";
    public string ControllerOffice { get; set; } = "Office of the Controller of Examinations";
    public string ContactEmail { get; set; } = "info@saluexamportal.edu.pk";
    public string ContactPhone { get; set; } = "0243-9280051";
    public string SupportAddress { get; set; } = "Shah Abdul Latif University Main Campus, Khairpur, Sindh, Pakistan";
    public string LogoUrl { get; set; } = "images/salu-logo.png";
}

public sealed class EnrollmentSettings
{
    public bool IsEnrollmentOpen { get; set; } = true;
    public DateTime? EnrollmentStartDate { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime? EnrollmentEndDate { get; set; } = new DateTime(2026, 10, 15, 23, 59, 59, DateTimeKind.Utc);
    public DateTime? EnrollmentLateFeeEndDate { get; set; } = new DateTime(2026, 11, 15, 23, 59, 59, DateTimeKind.Utc);
    public bool IsLateEnrollmentAllowed { get; set; } = true;
    public decimal BaseEnrollmentFee { get; set; } = 3840.00m;
    public decimal LateEnrollmentFee { get; set; } = 1000.00m;
    public decimal MigrationNocFee { get; set; } = 1000.00m;
    public int ChallanValidityDays { get; set; } = 7;
    public long MaxUploadSizeMb { get; set; } = 5;
    public string AllowedFileExtensions { get; set; } = ".jpg,.jpeg,.png,.pdf";
    public string CollectionBankName { get; set; } = "Habib Bank Limited (HBL) / National Bank of Pakistan (NBP)";
    public string CollectionAccountTitle { get; set; } = "SALU Exam Collection";
    public string CollectionAccountNumber { get; set; } = "00427900185903";
    public string CollectionIban { get; set; } = "PK12HABB0000427900185903";
    public string AffiliatedDistrictsCsv { get; set; } = "Sukkur,Khairpur,Ghotki,Shikarpur,Jacobabad,Kandhkot,Kashmore,Larkana,Shahdadkot,Qambar-Shahdadkot,Qambar Shahdadkot,Naushahro Feroze,Dadu";
}

public sealed class ExaminationSettings
{
    public bool IsExamRegistrationOpen { get; set; } = true;
    public DateTime? ExamStartDate { get; set; } = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    public DateTime? ExamEndDate { get; set; } = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
    public DateTime? ExamLateFeeEndDate { get; set; } = new DateTime(2026, 12, 15, 23, 59, 59, DateTimeKind.Utc);
    public bool IsLateExamRegistrationAllowed { get; set; } = true;
    public decimal BaseExamFee { get; set; } = 2500.00m;
    public decimal LateExamFee { get; set; } = 500.00m;
    public bool AutoGenerateAdmitCards { get; set; } = true;
    public bool IsResultPublicationActive { get; set; } = false;
    public bool AntiSkipGatekeeperEnforced { get; set; } = true;
}

public sealed class DocumentRequirementsSettings
{
    public bool PhotoRequired { get; set; } = true;
    public bool CnicRequired { get; set; } = true;
    public bool IntermediateMarksheetRequired { get; set; } = true;
    public bool MatricCertificateRequired { get; set; } = false;
    public bool DomicilePrcRequired { get; set; } = false;
    public bool OcrEnabled { get; set; } = true;
    public bool AutoVerificationEnabled { get; set; } = true;
    public int MinimumAuthenticityConfidenceScore { get; set; } = 65;
}

public sealed class SecurityAndFeatureSettings
{
    public bool MaintenanceMode { get; set; } = false;
    public string MaintenanceNotice { get; set; } = "System undergoing scheduled routine maintenance. Please check back shortly.";
    public bool RequireTwoFactorForAdmin { get; set; } = false;
    public bool AllowOnlinePayments { get; set; } = false;
    public bool SendEmailNotifications { get; set; } = true;
    public bool SendSmsNotifications { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 60;
}
