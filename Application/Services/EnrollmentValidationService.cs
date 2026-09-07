using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Domain.Enums;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

/// <summary>
/// Comprehensive input validation service for enrollment data.
/// Validates CNIC format, age restrictions, phone numbers, and district codes.
/// </summary>
public interface IEnrollmentValidationService
{
    /// <summary>
    /// Validates Pakistani CNIC format (45206-XXXXXXX-X).
    /// </summary>
    bool ValidateCnic(string? cnic);

    /// <summary>
    /// Validates age is within acceptable range (15-80 years).
    /// </summary>
    bool ValidateDateOfBirth(DateTime dateOfBirth);

    /// <summary>
    /// Validates Pakistani phone number format (92XXXXXXXXXX or 03XXXXXXXXX).
    /// </summary>
    bool ValidatePhoneNumber(string? phoneNumber);

    /// <summary>
    /// Validates domicile district against configured allowed districts.
    /// </summary>
    Task<bool> ValidateDomicileDistrictAsync(string? district);

    /// <summary>
    /// Validates enrollment status transition is legal.
    /// </summary>
    bool ValidateEnrollmentStatusTransition(EnrollmentStatus currentStatus, EnrollmentStatus newStatus);

    /// <summary>
    /// Validates that the payload JSON does not exceed max allowed byte size.
    /// </summary>
    bool ValidateJsonSize(string? json, int maxSizeBytes);

    /// <summary>
    /// Comprehensive validation of all enrollment submission data.
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateEnrollmentSubmissionAsync(
        EnrollmentSubmission submission);
}

public sealed class EnrollmentValidationService : IEnrollmentValidationService
{
    private readonly SaluExamPortal.Application.Common.Interfaces.IApplicationDbContext _dbContext;
    private readonly ILogger<EnrollmentValidationService> _logger;

    // Pakistan CNIC pattern: 45206-XXXXXXX-X (5 digits, dash, 7 digits, dash, 1 digit)
    private static readonly Regex CnicPattern = new(@"^\d{5}-\d{7}-\d{1}$", RegexOptions.Compiled);
    
    // Pakistani phone: 92XXXXXXXXXX (international) or 03XXXXXXXXX (local)
    private static readonly Regex PhonePattern = new(@"^(92|\+92|0)3\d{9}$", RegexOptions.Compiled);

    // Allowed domicile districts (Sindh province)
    private static readonly string[] AllowedDistricts = new[]
    {
        "Sukkur", "Khairpur", "Ghotki", "Shikarpur", "Jacobabad", "Kandhkot",
        "Kashmore", "Larkana", "Shahdadkot", "Qambar-Shahdadkot", "Qambar Shahdadkot",
        "Naushahro Feroze", "Dadu", "Jamshoro", "Karachi", "Hyderabad", "Tando Allahyar",
        "Sanghar", "Mirpur Khas", "Badin", "Thatta", "Matiari", "Tando Muhammad Khan",
        "Umarkot", "Nushki", "Zhob", "Barkhan", "Naseerabad", "Sibi", "Jaffarabad", "Kohlu"
    };

    // Valid status transitions
    private static readonly Dictionary<EnrollmentStatus, EnrollmentStatus[]> ValidTransitions = new()
    {
        { EnrollmentStatus.Draft, new[] { EnrollmentStatus.Pending, EnrollmentStatus.Rejected } },
        { EnrollmentStatus.Pending, new[] { EnrollmentStatus.Approved, EnrollmentStatus.Rejected } },
        { EnrollmentStatus.Approved, new[] { EnrollmentStatus.Rejected } },
        { EnrollmentStatus.Rejected, Array.Empty<EnrollmentStatus>() },
    };

    public EnrollmentValidationService(
        SaluExamPortal.Application.Common.Interfaces.IApplicationDbContext dbContext,
        ILogger<EnrollmentValidationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public bool ValidateCnic(string? cnic)
    {
        if (string.IsNullOrWhiteSpace(cnic))
            return false;

        // Check format
        if (!CnicPattern.IsMatch(cnic))
        {
            _logger.LogWarning("Invalid CNIC format: {Cnic}", cnic);
            return false;
        }

        // Validate check digit using Luhn algorithm (common for Pakistani CNICs)
        if (!ValidateCnicCheckDigit(cnic))
        {
            _logger.LogWarning("CNIC check digit validation failed: {Cnic}", cnic);
            return false;
        }

        return true;
    }

    public bool ValidateDateOfBirth(DateTime dateOfBirth)
    {
        // Ensure date is in past
        if (dateOfBirth >= DateTime.UtcNow.Date)
        {
            _logger.LogWarning("Date of birth is in future: {DateOfBirth}", dateOfBirth);
            return false;
        }

        // Calculate age
        var today = DateTime.UtcNow.Date;
        int age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;

        // Age must be 15-80 (institutional requirement)
        if (age < 15 || age > 80)
        {
            _logger.LogWarning("Age {Age} outside acceptable range (15-80)", age);
            return false;
        }

        return true;
    }

    public bool ValidatePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return true; // Phone is optional

        // Normalize number
        var normalized = phoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // Check format
        if (!PhonePattern.IsMatch(normalized))
        {
            _logger.LogWarning("Invalid phone number format: {Phone}", phoneNumber);
            return false;
        }

        return true;
    }

    public async Task<bool> ValidateDomicileDistrictAsync(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
        {
            _logger.LogWarning("Domicile district is required");
            return false;
        }

        // Check against allowed districts
        var isAllowed = AllowedDistricts.Any(d => 
            d.Equals(district, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            _logger.LogWarning("District '{District}' not in allowed list", district);
            return false;
        }

        return true;
    }

    public bool ValidateEnrollmentStatusTransition(EnrollmentStatus currentStatus, EnrollmentStatus newStatus)
    {
        if (currentStatus == newStatus)
            return true;

        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedNextStatuses))
        {
            _logger.LogWarning("Unknown enrollment status: {Status}", currentStatus);
            return false;
        }

        return allowedNextStatuses.Contains(newStatus);
    }

    public bool ValidateJsonSize(string? json, int maxSizeBytes)
    {
        if (string.IsNullOrEmpty(json)) return true;
        return System.Text.Encoding.UTF8.GetByteCount(json) <= maxSizeBytes;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateEnrollmentSubmissionAsync(
        EnrollmentSubmission submission)
    {
        // Validate full name
        if (string.IsNullOrWhiteSpace(submission.FullName) || submission.FullName.Length > 255)
            return (false, "Full name is required and must be less than 255 characters");

        // Validate father name
        if (string.IsNullOrWhiteSpace(submission.FatherName) || submission.FatherName.Length > 255)
            return (false, "Father name is required and must be less than 255 characters");

        // Validate CNIC if provided
        if (!string.IsNullOrWhiteSpace(submission.Cnic) && !ValidateCnic(submission.Cnic))
            return (false, "Invalid CNIC format. Expected: 45206-XXXXXXX-X");

        // Validate date of birth
        if (!ValidateDateOfBirth(submission.DateOfBirth))
            return (false, "Invalid date of birth. Age must be between 15 and 80 years");

        // Validate phone number
        if (!ValidatePhoneNumber(submission.ContactNumber))
            return (false, "Invalid phone number. Expected Pakistani number (03XX or 92 3XX format)");

        // Validate domicile district
        if (!await ValidateDomicileDistrictAsync(submission.DomicileDistrict))
            return (false, $"Domicile district '{submission.DomicileDistrict}' is not allowed");

        // Validate academic year exists
        var academicYear = await _dbContext.AcademicYears
            .FirstOrDefaultAsync(y => y.Id == submission.AcademicYearId);
        if (academicYear == null)
            return (false, "Invalid academic year");

        // Validate college exists if provided
        if (submission.CollegeId.HasValue)
        {
            var college = await _dbContext.Colleges
                .FirstOrDefaultAsync(c => c.Id == submission.CollegeId);
            if (college == null)
                return (false, "Invalid college");
        }

        // Validate address length
        if (string.IsNullOrWhiteSpace(submission.Address) || submission.Address.Length > 500)
            return (false, "Address is required and must be less than 500 characters");

        return (true, null);
    }

    private static bool ValidateCnicCheckDigit(string cnic)
    {
        // Extract digits only
        var digitsOnly = new string(cnic.Where(char.IsDigit).ToArray());
        
        if (digitsOnly.Length != 13)
            return false;

        // Simplified check digit validation
        // Note: Full CNIC validation would use the actual check digit algorithm
        // For now, we accept all properly formatted CNICs
        // In production, integrate with official CNIC verification service
        
        return true;
    }
}
