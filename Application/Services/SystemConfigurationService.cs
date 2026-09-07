using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Models;

namespace SaluExamPortal.Application.Services;

public enum WindowState
{
    BeforeOpening,
    RegularWindow,
    LateFeeWindow,
    Closed
}

public sealed record WindowEvaluationResult(
    WindowState State,
    bool IsActive,
    bool IsLateFeeApplied,
    decimal ApplicableBaseFee,
    decimal ApplicableLateFee,
    decimal TotalApplicableFee,
    DateTime? OpeningTime,
    DateTime? RegularClosingTime,
    DateTime? LateFeeClosingTime,
    TimeSpan? TimeRemaining,
    string StatusBadgeText,
    string StatusDetailMessage);

public interface ISystemConfigurationService
{
    Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken ct = default);
    Task<EnrollmentSettings> GetEnrollmentSettingsAsync(CancellationToken ct = default);
    Task<ExaminationSettings> GetExaminationSettingsAsync(CancellationToken ct = default);
    Task<DocumentRequirementsSettings> GetDocumentRequirementsAsync(CancellationToken ct = default);
    Task<SecurityAndFeatureSettings> GetSecuritySettingsAsync(CancellationToken ct = default);

    Task SaveGeneralSettingsAsync(GeneralSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default);
    Task SaveEnrollmentSettingsAsync(EnrollmentSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default);
    Task SaveExaminationSettingsAsync(ExaminationSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default);
    Task SaveDocumentRequirementsAsync(DocumentRequirementsSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default);
    Task SaveSecuritySettingsAsync(SecurityAndFeatureSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default);

    Task<bool> IsEnrollmentOpenAsync(CancellationToken ct = default);
    Task<bool> IsExaminationOpenAsync(CancellationToken ct = default);
    Task<bool> IsMaintenanceModeAsync(CancellationToken ct = default);

    Task<WindowEvaluationResult> GetEnrollmentWindowStatusAsync(CancellationToken ct = default);
    Task<WindowEvaluationResult> GetExaminationWindowStatusAsync(CancellationToken ct = default);

    Task<string> GetSettingValueAsync(string key, string defaultValue = "", CancellationToken ct = default);
    Task SetSettingValueAsync(string key, string value, ClaimsPrincipal? user = null, CancellationToken ct = default);
}

public sealed class SystemConfigurationService : ISystemConfigurationService
{
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemConfigurationService> _logger;
    private const string CacheKeyPrefix = "SALU_SYSTEM_CONFIG_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public SystemConfigurationService(
        IApplicationDbContext db,
        IMemoryCache cache,
        ILogger<SystemConfigurationService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyPrefix + "General", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return new GeneralSettings
            {
                UniversityName = await GetSettingInternalAsync("site_university_name", "Shah Abdul Latif University, Khairpur", ct),
                SystemName = await GetSettingInternalAsync("site_name", "Shah Abdul Latif University Exam Portal", ct),
                ControllerOffice = await GetSettingInternalAsync("site_controller_office", "Office of the Controller of Examinations", ct),
                ContactEmail = await GetSettingInternalAsync("site_email", "info@saluexamportal.edu.pk", ct),
                ContactPhone = await GetSettingInternalAsync("site_phone", "0243-9280051", ct),
                SupportAddress = await GetSettingInternalAsync("site_address", "Shah Abdul Latif University Main Campus, Khairpur, Sindh, Pakistan", ct),
                LogoUrl = await GetSettingInternalAsync("site_logo", "images/salu-logo.png", ct)
            };
        }) ?? new GeneralSettings();
    }

    public async Task<EnrollmentSettings> GetEnrollmentSettingsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyPrefix + "Enrollment", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return new EnrollmentSettings
            {
                IsEnrollmentOpen = bool.TryParse(await GetSettingInternalAsync("allow_enrollment", "true", ct), out var open) && open,
                EnrollmentStartDate = DateTime.TryParse(await GetSettingInternalAsync("enrollment_start_date", "2026-01-01T00:00:00Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var start) ? start : new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EnrollmentEndDate = DateTime.TryParse(await GetSettingInternalAsync("enrollment_end_date", "2026-10-15T23:59:59Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var end) ? end : new DateTime(2026, 10, 15, 23, 59, 59, DateTimeKind.Utc),
                EnrollmentLateFeeEndDate = DateTime.TryParse(await GetSettingInternalAsync("enrollment_late_end_date", "2026-11-15T23:59:59Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var lateEnd) ? lateEnd : new DateTime(2026, 11, 15, 23, 59, 59, DateTimeKind.Utc),
                IsLateEnrollmentAllowed = bool.TryParse(await GetSettingInternalAsync("allow_late_enrollment", "true", ct), out var late) && late,
                BaseEnrollmentFee = decimal.TryParse(await GetSettingInternalAsync("enrollment_fee_amount", "3840.00", ct), NumberStyles.Any, CultureInfo.InvariantCulture, out var baseFee) ? baseFee : 3840.00m,
                LateEnrollmentFee = decimal.TryParse(await GetSettingInternalAsync("late_enrollment_fee_amount", "1000.00", ct), NumberStyles.Any, CultureInfo.InvariantCulture, out var lateFee) ? lateFee : 1000.00m,
                MigrationNocFee = decimal.TryParse(await GetSettingInternalAsync("migration_noc_fee_amount", "1000.00", ct), NumberStyles.Any, CultureInfo.InvariantCulture, out var nocFee) ? nocFee : 1000.00m,
                ChallanValidityDays = int.TryParse(await GetSettingInternalAsync("challan_validity_days", "7", ct), out var days) ? days : 7,
                MaxUploadSizeMb = long.TryParse(await GetSettingInternalAsync("max_upload_size_mb", "5", ct), out var mb) ? mb : 5,
                AllowedFileExtensions = await GetSettingInternalAsync("allowed_file_extensions", ".jpg,.jpeg,.png,.pdf", ct)
            };
        }) ?? new EnrollmentSettings();
    }

    public async Task<ExaminationSettings> GetExaminationSettingsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyPrefix + "Examination", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return new ExaminationSettings
            {
                IsExamRegistrationOpen = bool.TryParse(await GetSettingInternalAsync("allow_exam_registration", "true", ct), out var open) && open,
                ExamStartDate = DateTime.TryParse(await GetSettingInternalAsync("exam_start_date", "2026-10-15T00:00:00Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var start) ? start : new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                ExamEndDate = DateTime.TryParse(await GetSettingInternalAsync("exam_end_date", "2026-11-30T23:59:59Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var end) ? end : new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc),
                ExamLateFeeEndDate = DateTime.TryParse(await GetSettingInternalAsync("exam_late_end_date", "2026-12-15T23:59:59Z", ct), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var lateEnd) ? lateEnd : new DateTime(2026, 12, 15, 23, 59, 59, DateTimeKind.Utc),
                IsLateExamRegistrationAllowed = bool.TryParse(await GetSettingInternalAsync("allow_late_exam", "true", ct), out var late) && late,
                BaseExamFee = decimal.TryParse(await GetSettingInternalAsync("base_exam_fee", "2500.00", ct), NumberStyles.Any, CultureInfo.InvariantCulture, out var fee) ? fee : 2500.00m,
                LateExamFee = decimal.TryParse(await GetSettingInternalAsync("late_exam_fee", "500.00", ct), NumberStyles.Any, CultureInfo.InvariantCulture, out var lFee) ? lFee : 500.00m,
                AutoGenerateAdmitCards = bool.TryParse(await GetSettingInternalAsync("auto_generate_admit_cards", "true", ct), out var auto) && auto,
                IsResultPublicationActive = bool.TryParse(await GetSettingInternalAsync("results_published", "false", ct), out var res) && res,
                AntiSkipGatekeeperEnforced = bool.TryParse(await GetSettingInternalAsync("anti_skip_enforced", "true", ct), out var anti) && anti
            };
        }) ?? new ExaminationSettings();
    }

    public async Task<DocumentRequirementsSettings> GetDocumentRequirementsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyPrefix + "Documents", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return new DocumentRequirementsSettings
            {
                PhotoRequired = bool.TryParse(await GetSettingInternalAsync("req_photo", "true", ct), out var p) && p,
                CnicRequired = bool.TryParse(await GetSettingInternalAsync("req_cnic", "true", ct), out var c) && c,
                IntermediateMarksheetRequired = bool.TryParse(await GetSettingInternalAsync("req_inter", "true", ct), out var i) && i,
                MatricCertificateRequired = bool.TryParse(await GetSettingInternalAsync("req_matric", "false", ct), out var m) && m,
                DomicilePrcRequired = bool.TryParse(await GetSettingInternalAsync("req_domicile", "false", ct), out var d) && d,
                OcrEnabled = bool.TryParse(await GetSettingInternalAsync("ocr_enabled", "true", ct), out var o) && o,
                AutoVerificationEnabled = bool.TryParse(await GetSettingInternalAsync("ocr_auto_verify", "true", ct), out var v) && v,
                MinimumAuthenticityConfidenceScore = int.TryParse(await GetSettingInternalAsync("ocr_min_score", "65", ct), out var s) ? s : 65
            };
        }) ?? new DocumentRequirementsSettings();
    }

    public async Task<SecurityAndFeatureSettings> GetSecuritySettingsAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyPrefix + "Security", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return new SecurityAndFeatureSettings
            {
                MaintenanceMode = bool.TryParse(await GetSettingInternalAsync("maintenance_mode", "false", ct), out var m) && m,
                MaintenanceNotice = await GetSettingInternalAsync("maintenance_notice", "System undergoing scheduled routine maintenance. Please check back shortly.", ct),
                RequireTwoFactorForAdmin = bool.TryParse(await GetSettingInternalAsync("req_admin_2fa", "false", ct), out var tfa) && tfa,
                AllowOnlinePayments = bool.TryParse(await GetSettingInternalAsync("allow_online_payments", "false", ct), out var pay) && pay,
                SendEmailNotifications = bool.TryParse(await GetSettingInternalAsync("send_email", "true", ct), out var em) && em,
                SendSmsNotifications = bool.TryParse(await GetSettingInternalAsync("send_sms", "false", ct), out var sms) && sms,
                SessionTimeoutMinutes = int.TryParse(await GetSettingInternalAsync("session_timeout", "60", ct), out var timeout) ? timeout : 60
            };
        }) ?? new SecurityAndFeatureSettings();
    }

    public async Task<WindowEvaluationResult> GetEnrollmentWindowStatusAsync(CancellationToken ct = default)
    {
        var settings = await GetEnrollmentSettingsAsync(ct);
        var now = DateTime.UtcNow;

        if (!settings.IsEnrollmentOpen)
        {
            return new WindowEvaluationResult(
                WindowState.Closed,
                false,
                false,
                settings.BaseEnrollmentFee,
                0,
                settings.BaseEnrollmentFee,
                settings.EnrollmentStartDate,
                settings.EnrollmentEndDate,
                settings.EnrollmentLateFeeEndDate,
                null,
                "PORTAL CLOSED",
                "Enrollment submissions are currently disabled by the Controller of Examinations.");
        }

        if (settings.EnrollmentStartDate.HasValue && now < settings.EnrollmentStartDate.Value)
        {
            var diff = settings.EnrollmentStartDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.BeforeOpening,
                false,
                false,
                settings.BaseEnrollmentFee,
                0,
                settings.BaseEnrollmentFee,
                settings.EnrollmentStartDate,
                settings.EnrollmentEndDate,
                settings.EnrollmentLateFeeEndDate,
                diff,
                "OPENING SOON",
                $"Enrollment portal opens on {settings.EnrollmentStartDate.Value:dd MMM yyyy HH:mm} UTC (in {diff.Days} days {diff.Hours} hrs).");
        }

        if (settings.EnrollmentEndDate.HasValue && now <= settings.EnrollmentEndDate.Value)
        {
            var diff = settings.EnrollmentEndDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.RegularWindow,
                true,
                false,
                settings.BaseEnrollmentFee,
                0,
                settings.BaseEnrollmentFee,
                settings.EnrollmentStartDate,
                settings.EnrollmentEndDate,
                settings.EnrollmentLateFeeEndDate,
                diff,
                "REGULAR WINDOW OPEN",
                $"Regular enrollment open at standard fee (Rs. {settings.BaseEnrollmentFee:N0}). Closes on {settings.EnrollmentEndDate.Value:dd MMM yyyy} ({diff.Days} days {diff.Hours} hrs remaining).");
        }

        // Check Late Window
        if (settings.IsLateEnrollmentAllowed && settings.EnrollmentLateFeeEndDate.HasValue && now <= settings.EnrollmentLateFeeEndDate.Value)
        {
            var diff = settings.EnrollmentLateFeeEndDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.LateFeeWindow,
                true,
                true,
                settings.BaseEnrollmentFee,
                settings.LateEnrollmentFee,
                settings.BaseEnrollmentFee + settings.LateEnrollmentFee,
                settings.EnrollmentStartDate,
                settings.EnrollmentEndDate,
                settings.EnrollmentLateFeeEndDate,
                diff,
                "LATE FEE WINDOW ACTIVE",
                $"Late submission window active! Late surcharge fee of Rs. {settings.LateEnrollmentFee:N0} automatically applied. Final cutoff: {settings.EnrollmentLateFeeEndDate.Value:dd MMM yyyy} ({diff.Days} days {diff.Hours} hrs remaining).");
        }

        return new WindowEvaluationResult(
            WindowState.Closed,
            false,
            false,
            settings.BaseEnrollmentFee,
            0,
            settings.BaseEnrollmentFee,
            settings.EnrollmentStartDate,
            settings.EnrollmentEndDate,
            settings.EnrollmentLateFeeEndDate,
            null,
            "DEADLINE EXPIRED",
            "The enrollment deadline has passed. Submissions are closed.");
    }

    public async Task<WindowEvaluationResult> GetExaminationWindowStatusAsync(CancellationToken ct = default)
    {
        var settings = await GetExaminationSettingsAsync(ct);
        var now = DateTime.UtcNow;

        if (!settings.IsExamRegistrationOpen)
        {
            return new WindowEvaluationResult(
                WindowState.Closed,
                false,
                false,
                settings.BaseExamFee,
                0,
                settings.BaseExamFee,
                settings.ExamStartDate,
                settings.ExamEndDate,
                settings.ExamLateFeeEndDate,
                null,
                "EXAM REGISTRATION CLOSED",
                "Examination registration is currently closed.");
        }

        if (settings.ExamStartDate.HasValue && now < settings.ExamStartDate.Value)
        {
            var diff = settings.ExamStartDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.BeforeOpening,
                false,
                false,
                settings.BaseExamFee,
                0,
                settings.BaseExamFee,
                settings.ExamStartDate,
                settings.ExamEndDate,
                settings.ExamLateFeeEndDate,
                diff,
                "EXAMS OPENING SOON",
                $"Exam registration opens on {settings.ExamStartDate.Value:dd MMM yyyy}.");
        }

        if (settings.ExamEndDate.HasValue && now <= settings.ExamEndDate.Value)
        {
            var diff = settings.ExamEndDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.RegularWindow,
                true,
                false,
                settings.BaseExamFee,
                0,
                settings.BaseExamFee,
                settings.ExamStartDate,
                settings.ExamEndDate,
                settings.ExamLateFeeEndDate,
                diff,
                "EXAM REGISTRATION OPEN",
                $"Regular exam registration open at standard fee (Rs. {settings.BaseExamFee:N0}). Closes on {settings.ExamEndDate.Value:dd MMM yyyy}.");
        }

        if (settings.IsLateExamRegistrationAllowed && settings.ExamLateFeeEndDate.HasValue && now <= settings.ExamLateFeeEndDate.Value)
        {
            var diff = settings.ExamLateFeeEndDate.Value - now;
            return new WindowEvaluationResult(
                WindowState.LateFeeWindow,
                true,
                true,
                settings.BaseExamFee,
                settings.LateExamFee,
                settings.BaseExamFee + settings.LateExamFee,
                settings.ExamStartDate,
                settings.ExamEndDate,
                settings.ExamLateFeeEndDate,
                diff,
                "LATE EXAM WINDOW ACTIVE",
                $"Late exam registration active! Surcharge fee of Rs. {settings.LateExamFee:N0} auto-applied. Cutoff: {settings.ExamLateFeeEndDate.Value:dd MMM yyyy}.");
        }

        return new WindowEvaluationResult(
            WindowState.Closed,
            false,
            false,
            settings.BaseExamFee,
            0,
            settings.BaseExamFee,
            settings.ExamStartDate,
            settings.ExamEndDate,
            settings.ExamLateFeeEndDate,
            null,
            "EXAM DEADLINE EXPIRED",
            "The examination registration deadline has passed.");
    }

    public async Task SaveGeneralSettingsAsync(GeneralSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await SetSettingInternalAsync("site_university_name", settings.UniversityName, user, ct);
        await SetSettingInternalAsync("site_name", settings.SystemName, user, ct);
        await SetSettingInternalAsync("site_controller_office", settings.ControllerOffice, user, ct);
        await SetSettingInternalAsync("site_email", settings.ContactEmail, user, ct);
        await SetSettingInternalAsync("site_phone", settings.ContactPhone, user, ct);
        await SetSettingInternalAsync("site_address", settings.SupportAddress, user, ct);
        await SetSettingInternalAsync("site_logo", settings.LogoUrl, user, ct);

        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "General");
    }

    public async Task SaveEnrollmentSettingsAsync(EnrollmentSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.EnrollmentStartDate.HasValue && settings.EnrollmentEndDate.HasValue &&
            settings.EnrollmentEndDate.Value < settings.EnrollmentStartDate.Value)
        {
            throw new InvalidOperationException("Enrollment closing date cannot be before the opening date.");
        }

        if (settings.EnrollmentEndDate.HasValue && settings.EnrollmentLateFeeEndDate.HasValue &&
            settings.EnrollmentLateFeeEndDate.Value < settings.EnrollmentEndDate.Value)
        {
            throw new InvalidOperationException("Late fee extended closing date cannot be before the regular closing date.");
        }

        if (settings.BaseEnrollmentFee < 0 || settings.LateEnrollmentFee < 0 || settings.MigrationNocFee < 0)
        {
            throw new InvalidOperationException("Fee amounts cannot be negative.");
        }

        await SetSettingInternalAsync("allow_enrollment", settings.IsEnrollmentOpen.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("enrollment_start_date", settings.EnrollmentStartDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("enrollment_end_date", settings.EnrollmentEndDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("enrollment_late_end_date", settings.EnrollmentLateFeeEndDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("allow_late_enrollment", settings.IsLateEnrollmentAllowed.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("enrollment_fee_amount", settings.BaseEnrollmentFee.ToString("F2", CultureInfo.InvariantCulture), user, ct);
        await SetSettingInternalAsync("late_enrollment_fee_amount", settings.LateEnrollmentFee.ToString("F2", CultureInfo.InvariantCulture), user, ct);
        await SetSettingInternalAsync("migration_noc_fee_amount", settings.MigrationNocFee.ToString("F2", CultureInfo.InvariantCulture), user, ct);
        await SetSettingInternalAsync("challan_validity_days", settings.ChallanValidityDays.ToString(), user, ct);
        await SetSettingInternalAsync("max_upload_size_mb", settings.MaxUploadSizeMb.ToString(), user, ct);
        await SetSettingInternalAsync("allowed_file_extensions", settings.AllowedFileExtensions, user, ct);

        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "Enrollment");
    }

    public async Task SaveExaminationSettingsAsync(ExaminationSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.ExamStartDate.HasValue && settings.ExamEndDate.HasValue &&
            settings.ExamEndDate.Value < settings.ExamStartDate.Value)
        {
            throw new InvalidOperationException("Examination closing date cannot be before the opening date.");
        }

        if (settings.ExamEndDate.HasValue && settings.ExamLateFeeEndDate.HasValue &&
            settings.ExamLateFeeEndDate.Value < settings.ExamEndDate.Value)
        {
            throw new InvalidOperationException("Exam late fee extended cutoff cannot be before the regular closing date.");
        }

        if (settings.BaseExamFee < 0 || settings.LateExamFee < 0)
        {
            throw new InvalidOperationException("Examination fee amounts cannot be negative.");
        }

        await SetSettingInternalAsync("allow_exam_registration", settings.IsExamRegistrationOpen.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("exam_start_date", settings.ExamStartDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("exam_end_date", settings.ExamEndDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("exam_late_end_date", settings.ExamLateFeeEndDate?.ToString("o", CultureInfo.InvariantCulture) ?? "", user, ct);
        await SetSettingInternalAsync("allow_late_exam", settings.IsLateExamRegistrationAllowed.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("base_exam_fee", settings.BaseExamFee.ToString("F2", CultureInfo.InvariantCulture), user, ct);
        await SetSettingInternalAsync("late_exam_fee", settings.LateExamFee.ToString("F2", CultureInfo.InvariantCulture), user, ct);
        await SetSettingInternalAsync("auto_generate_admit_cards", settings.AutoGenerateAdmitCards.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("results_published", settings.IsResultPublicationActive.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("anti_skip_enforced", settings.AntiSkipGatekeeperEnforced.ToString().ToLowerInvariant(), user, ct);

        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "Examination");
    }

    public async Task SaveDocumentRequirementsAsync(DocumentRequirementsSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.MinimumAuthenticityConfidenceScore = Math.Clamp(settings.MinimumAuthenticityConfidenceScore, 10, 100);

        await SetSettingInternalAsync("req_photo", settings.PhotoRequired.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("req_cnic", settings.CnicRequired.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("req_inter", settings.IntermediateMarksheetRequired.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("req_matric", settings.MatricCertificateRequired.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("req_domicile", settings.DomicilePrcRequired.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("ocr_enabled", settings.OcrEnabled.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("ocr_auto_verify", settings.AutoVerificationEnabled.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("ocr_min_score", settings.MinimumAuthenticityConfidenceScore.ToString(), user, ct);

        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "Documents");
    }

    public async Task SaveSecuritySettingsAsync(SecurityAndFeatureSettings settings, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await SetSettingInternalAsync("maintenance_mode", settings.MaintenanceMode.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("maintenance_notice", settings.MaintenanceNotice, user, ct);
        await SetSettingInternalAsync("req_admin_2fa", settings.RequireTwoFactorForAdmin.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("allow_online_payments", settings.AllowOnlinePayments.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("send_email", settings.SendEmailNotifications.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("send_sms", settings.SendSmsNotifications.ToString().ToLowerInvariant(), user, ct);
        await SetSettingInternalAsync("session_timeout", settings.SessionTimeoutMinutes.ToString(), user, ct);

        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "Security");
    }

    public async Task<bool> IsEnrollmentOpenAsync(CancellationToken ct = default)
    {
        var status = await GetEnrollmentWindowStatusAsync(ct);
        return status.IsActive;
    }

    public async Task<bool> IsExaminationOpenAsync(CancellationToken ct = default)
    {
        var status = await GetExaminationWindowStatusAsync(ct);
        return status.IsActive;
    }

    public async Task<bool> IsMaintenanceModeAsync(CancellationToken ct = default)
    {
        var settings = await GetSecuritySettingsAsync(ct);
        return settings.MaintenanceMode;
    }

    public async Task<string> GetSettingValueAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        return await GetSettingInternalAsync(key, defaultValue, ct);
    }

    public async Task SetSettingValueAsync(string key, string value, ClaimsPrincipal? user = null, CancellationToken ct = default)
    {
        await SetSettingInternalAsync(key, value, user, ct);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyPrefix + "General");
        _cache.Remove(CacheKeyPrefix + "Enrollment");
        _cache.Remove(CacheKeyPrefix + "Examination");
        _cache.Remove(CacheKeyPrefix + "Documents");
        _cache.Remove(CacheKeyPrefix + "Security");
    }

    private async Task<string> GetSettingInternalAsync(string key, string defaultValue, CancellationToken ct)
    {
        var setting = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value ?? defaultValue;
    }

    private async Task SetSettingInternalAsync(string key, string value, ClaimsPrincipal? user, CancellationToken ct)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        string? oldValue = setting?.Value;

        if (setting == null)
        {
            setting = new SystemSetting { Key = key, Value = value };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        if (oldValue != value)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "SystemAdmin",
                Action = "SettingChanged",
                Entity = "SystemSetting",
                EntityId = key,
                Details = $"Key '{key}' updated from '{oldValue}' to '{value}'.",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
