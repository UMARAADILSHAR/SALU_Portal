using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public interface IEmailOtpService
{
    Task<(bool Succeeded, string Code, string? ErrorMessage)> GenerateAndSendOtpAsync(ApplicationUser user, string email);
    Task<(bool Succeeded, string ErrorMessage)> ValidateOtpAsync(ApplicationUser user, string inputCode);
    Task<(bool Succeeded, string Message)> ResendOtpAsync(string email);
    Task<bool> HasActiveOtpAsync(ApplicationUser user);
}

public class EmailOtpService : IEmailOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BrevoEmailSender _emailSender;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmailOtpService> _logger;

    private const string TokenProvider = "EmailOtp";
    private const string CodePurpose = "VerificationCode";
    private const string ExpiryPurpose = "Expiry";
    private const string AttemptsPurpose = "Attempts";
    private const int OtpValidityMinutes = 30; // Half hour expiry
    private const int MaxAttempts = 5;

    public EmailOtpService(
        UserManager<ApplicationUser> userManager,
        BrevoEmailSender emailSender,
        IMemoryCache cache,
        ILogger<EmailOtpService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(bool Succeeded, string Code, string? ErrorMessage)> GenerateAndSendOtpAsync(ApplicationUser user, string email)
    {
        // Generate 6-digit cryptographically random OTP
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiry = DateTime.UtcNow.AddMinutes(OtpValidityMinutes);

        // Store in AspNetUserTokens table (persistent across restarts, zero DB migration needed)
        await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, CodePurpose, code);
        await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, ExpiryPurpose, expiry.ToString("O"));
        await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, AttemptsPurpose, "0");

        // Also store in fast cache
        var cacheKey = $"EmailOtp_{user.Id}";
        _cache.Set(cacheKey, (Code: code, Expiry: expiry, Attempts: 0), expiry);

        // Send institutional OTP email via Brevo
        var (succeeded, error) = await _emailSender.SendEmailOtpAsync(user, email, code, OtpValidityMinutes);
        if (!succeeded)
        {
            _logger.LogError("Failed to dispatch OTP email to {Email}: {Error}", email, error);
            return (false, code, error);
        }

        _logger.LogInformation("Generated and sent 6-digit OTP to {Email} with expiry at {Expiry} (30 mins)", email, expiry);
        return (true, code, null);
    }

    public async Task<bool> HasActiveOtpAsync(ApplicationUser user)
    {
        var storedCode = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, CodePurpose);
        var expiryStr = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, ExpiryPurpose);

        if (string.IsNullOrWhiteSpace(storedCode) || string.IsNullOrWhiteSpace(expiryStr))
        {
            return false;
        }

        if (!DateTime.TryParse(expiryStr, out var expiry) || DateTime.UtcNow > expiry)
        {
            return false;
        }

        return true;
    }

    public async Task<(bool Succeeded, string ErrorMessage)> ValidateOtpAsync(ApplicationUser user, string inputCode)
    {
        if (string.IsNullOrWhiteSpace(inputCode))
        {
            return (false, "Please enter the 6-digit verification code.");
        }

        var cleanCode = new string(inputCode.Where(char.IsDigit).ToArray());
        if (cleanCode.Length != 6)
        {
            return (false, "Verification code must be exactly 6 digits.");
        }

        // Retrieve from tokens table
        var storedCode = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, CodePurpose);
        var expiryStr = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, ExpiryPurpose);
        var attemptsStr = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, AttemptsPurpose);

        if (string.IsNullOrWhiteSpace(storedCode) || string.IsNullOrWhiteSpace(expiryStr))
        {
            return (false, "No active verification code found. Please click 'Resend Code' to request a new one.");
        }

        int attempts = 0;
        int.TryParse(attemptsStr, out attempts);

        if (attempts >= MaxAttempts)
        {
            await ClearOtpAsync(user);
            return (false, "Too many failed attempts. For your security, this code has been revoked. Please request a new code.");
        }

        if (!DateTime.TryParse(expiryStr, out var expiry) || DateTime.UtcNow > expiry)
        {
            await ClearOtpAsync(user);
            return (false, "This verification code has expired (exceeded 30 minutes). Please request a new code.");
        }

        if (!string.Equals(storedCode, cleanCode, StringComparison.Ordinal))
        {
            attempts++;
            await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, AttemptsPurpose, attempts.ToString());
            var remaining = MaxAttempts - attempts;
            return (false, $"Invalid verification code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        }

        // Code matches and is within 30 minutes!
        user.EmailConfirmed = true;
        user.IsVerified = true;
        await _userManager.UpdateAsync(user);

        // Clear used tokens
        await ClearOtpAsync(user);
        _logger.LogInformation("Successfully verified email OTP for user {UserId} ({Email})", user.Id, user.Email);

        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Message)> ResendOtpAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email address is required.");
        }

        var cleanEmail = email.Trim();
        var user = await _userManager.FindByEmailAsync(cleanEmail);
        if (user is null)
        {
            // Do not reveal user existence
            return (true, "If an account is associated with this email, a fresh 6-digit code has been sent.");
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            return (false, "Your email is already verified. You can sign in directly.");
        }

        var (succeeded, _, error) = await GenerateAndSendOtpAsync(user, cleanEmail);
        if (!succeeded)
        {
            return (false, !string.IsNullOrWhiteSpace(error) ? error : "Could not send verification email. Please try again.");
        }

        return (true, "A fresh 6-digit verification code has been sent to your email (valid for 30 minutes).");
    }

    private async Task ClearOtpAsync(ApplicationUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, CodePurpose);
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, ExpiryPurpose);
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, AttemptsPurpose);
        _cache.Remove($"EmailOtp_{user.Id}");
    }
}
