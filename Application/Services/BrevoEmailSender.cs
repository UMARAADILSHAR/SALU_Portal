using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public class BrevoOptions
{
    public const string SectionName = "Brevo";
    public string ApiKey { get; set; } = "";
    public string SmtpUser { get; set; } = "saluexamportal@gmail.com";
    public string SmtpServer { get; set; } = "smtp-relay.brevo.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "saluexamportal@gmail.com";
    public string SenderName { get; set; } = "Shah Abdul Latif University - Admissions";
}

public class BrevoEmailSender : IEmailSender<ApplicationUser>, IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient httpClient, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await SendBrevoEmailAsync(email, null, subject, htmlMessage);
    }

    public async Task SendEmailOtpAsync(ApplicationUser user, string email, string otpCode, int expirationMinutes = 30)
    {
        var studentName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "Candidate";
        var subject = $"{otpCode} is your SALU Admission Verification Code";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{subject}</title>
    <style>
        body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
        .container {{ max-width: 580px; margin: 30px auto; background-color: #ffffff; border-radius: 14px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }}
        .header {{ background: linear-gradient(135deg, #1b2a6b 0%, #0d1b4c 100%); padding: 32px 24px; text-align: center; color: #ffffff; }}
        .logo-title {{ font-size: 20px; font-weight: 800; letter-spacing: 0.5px; margin: 0; color: #ffffff; }}
        .logo-sub {{ font-size: 13px; color: #fbbf24; margin-top: 4px; font-weight: 600; text-transform: uppercase; letter-spacing: 1px; }}
        .content {{ padding: 36px 32px; color: #334155; line-height: 1.6; font-size: 15px; }}
        .greeting {{ font-size: 18px; font-weight: 700; color: #0f172a; margin-bottom: 14px; }}
        .otp-container {{ text-align: center; margin: 28px 0; }}
        .otp-box {{ display: inline-block; background: #eff6ff; border: 2px dashed #2563eb; border-radius: 12px; padding: 18px 36px; box-shadow: 0 4px 12px rgba(37,99,235,0.1); }}
        .otp-code {{ font-family: 'Courier New', Courier, monospace; font-size: 38px; font-weight: 800; letter-spacing: 12px; color: #1e3a8a; margin-left: 12px; }}
        .expiry-note {{ font-size: 13px; color: #b91c1c; font-weight: 700; margin-top: 10px; }}
        .security-box {{ font-size: 13px; color: #64748b; background-color: #f8fafc; padding: 14px 18px; border-left: 4px solid #3b82f6; border-radius: 6px; margin-top: 24px; line-height: 1.5; }}
        .footer {{ background-color: #f8fafc; padding: 20px 24px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo-title'>SHAH ABDUL LATIF UNIVERSITY, KHAIRPUR</div>
            <div class='logo-sub'>Directorate of Admissions &bull; Email Verification</div>
        </div>
        <div class='content'>
            <div class='greeting'>Dear {studentName},</div>
            <p>Thank you for registering at Shah Abdul Latif University Admission Portal. Please use the following <strong>6-digit verification code (OTP)</strong> to activate your candidate account:</p>
            
            <div class='otp-container'>
                <div class='otp-box'>
                    <div class='otp-code'>{otpCode}</div>
                </div>
                <div class='expiry-note'>&#x23F1; This code expires in {expirationMinutes} minutes (half an hour).</div>
            </div>

            <div class='security-box'>
                <strong>Important Security Note:</strong><br />
                Do not share this OTP with anyone, including university staff. If you did not initiate this registration, please disregard this email.
            </div>
        </div>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} Shah Abdul Latif University, Khairpur, Sindh, Pakistan.<br />
            This is an automated institutional message.
        </div>
    </div>
</body>
</html>";

        await SendBrevoEmailAsync(email, studentName, subject, body);
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var studentName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "Candidate";
        var subject = "Confirm your SALU Admission Account Email";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{subject}</title>
    <style>
        body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
        .container {{ max-width: 600px; margin: 30px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; }}
        .header {{ background: linear-gradient(135deg, #1b2a6b 0%, #0d1b4c 100%); padding: 32px 24px; text-align: center; color: #ffffff; }}
        .logo-title {{ font-size: 20px; font-weight: 800; letter-spacing: 0.5px; margin: 0; color: #ffffff; }}
        .logo-sub {{ font-size: 13px; color: #fbbf24; margin-top: 4px; font-weight: 600; text-transform: uppercase; letter-spacing: 1px; }}
        .content {{ padding: 36px 32px; color: #334155; line-height: 1.6; font-size: 15px; }}
        .greeting {{ font-size: 18px; font-weight: 700; color: #0f172a; margin-bottom: 16px; }}
        .btn-wrapper {{ text-align: center; margin: 32px 0; }}
        .btn {{ display: inline-block; background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); color: #ffffff !important; padding: 14px 36px; border-radius: 8px; font-weight: 700; font-size: 16px; text-decoration: none; box-shadow: 0 4px 14px rgba(37,99,235,0.3); }}
        .note {{ font-size: 13px; color: #64748b; background-color: #f8fafc; padding: 14px 18px; border-left: 4px solid #3b82f6; border-radius: 4px; margin-top: 24px; word-break: break-all; }}
        .footer {{ background-color: #f8fafc; padding: 20px 24px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo-title'>SHAH ABDUL LATIF UNIVERSITY, KHAIRPUR</div>
            <div class='logo-sub'>Directorate of Admissions</div>
        </div>
        <div class='content'>
            <div class='greeting'>Dear {studentName},</div>
            <p>Thank you for registering at Shah Abdul Latif University Admission Portal. To complete your account activation and proceed with your admission application, please confirm your email address.</p>
            
            <div class='btn-wrapper'>
                <a href='{confirmationLink}' class='btn' target='_blank'>Verify Email Address</a>
            </div>

            <p style='font-size: 14px; color: #475569;'>If the button above does not work, you can copy and paste the following link into your browser:</p>
            <div class='note'>
                {confirmationLink}
            </div>

            <p style='margin-top: 28px; font-size: 13px; color: #64748b;'>
                If you did not initiate this registration, please disregard this email. This link will expire for security purposes.
            </p>
        </div>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} Shah Abdul Latif University, Khairpur, Sindh, Pakistan.<br />
            This is an automated institutional message. Please do not reply directly to this email.
        </div>
    </div>
</body>
</html>";

        await SendBrevoEmailAsync(email, studentName, subject, body);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var studentName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "User";
        var subject = "Reset your SALU Admission Account Password";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ margin: 0; padding: 0; background-color: #f1f5f9; font-family: 'Segoe UI', sans-serif; }}
        .container {{ max-width: 600px; margin: 30px auto; background: #fff; border-radius: 12px; overflow: hidden; border: 1px solid #e2e8f0; }}
        .header {{ background: #1b2a6b; padding: 24px; text-align: center; color: #fff; font-size: 18px; font-weight: bold; }}
        .content {{ padding: 32px; color: #334155; line-height: 1.6; }}
        .btn {{ display: inline-block; background: #dc2626; color: #ffffff !important; padding: 12px 28px; border-radius: 6px; font-weight: bold; text-decoration: none; margin: 20px 0; }}
        .footer {{ background: #f8fafc; padding: 16px; text-align: center; font-size: 12px; color: #94a3b8; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>SHAH ABDUL LATIF UNIVERSITY &bull; PASSWORD RESET</div>
        <div class='content'>
            <p>Dear {studentName},</p>
            <p>We received a request to reset your password for your SALU account. Click the button below to set a new password:</p>
            <p style='text-align: center;'><a href='{resetLink}' class='btn' target='_blank'>Reset My Password</a></p>
            <p style='font-size: 13px; color: #64748b;'>If you did not request this, your account is safe and you can ignore this email.</p>
        </div>
        <div class='footer'>&copy; {DateTime.UtcNow.Year} Shah Abdul Latif University, Khairpur</div>
    </div>
</body>
</html>";

        await SendBrevoEmailAsync(email, studentName, subject, body);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Your SALU Password Reset Code";
        var body = $"<p>Your password reset code for Shah Abdul Latif University portal is: <strong>{resetCode}</strong></p>";
        await SendBrevoEmailAsync(email, user.FullName, subject, body);
    }

    private async Task SendBrevoEmailAsync(string toEmail, string? toName, string subject, string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Brevo API/SMTP Key is not configured. Email to {ToEmail} skipped.", toEmail);
            return;
        }

        if (_options.ApiKey.StartsWith("xsmtpsib-", StringComparison.OrdinalIgnoreCase))
        {
            await SendViaSmtpRelayAsync(toEmail, toName, subject, htmlContent);
            return;
        }

        await SendViaRestApiAsync(toEmail, toName, subject, htmlContent);
    }

    private async Task SendViaSmtpRelayAsync(string toEmail, string? toName, string subject, string htmlContent)
    {
        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(_options.SmtpServer, _options.SmtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(
                    !string.IsNullOrWhiteSpace(_options.SmtpUser) ? _options.SmtpUser : _options.SenderEmail,
                    _options.ApiKey
                ),
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                Timeout = 20000
            };

            using var mail = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(_options.SenderEmail, _options.SenderName),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };
            mail.To.Add(new System.Net.Mail.MailAddress(toEmail, toName ?? toEmail));

            await smtp.SendMailAsync(mail);
            _logger.LogInformation("Verification email sent successfully via Brevo SMTP relay to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Brevo SMTP relay to {Email}", toEmail);
        }
    }

    private async Task SendViaRestApiAsync(string toEmail, string? toName, string subject, string htmlContent)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _options.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                sender = new
                {
                    name = _options.SenderName,
                    email = _options.SenderEmail
                },
                to = new[]
                {
                    new
                    {
                        email = toEmail,
                        name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName
                    }
                },
                subject = subject,
                htmlContent = htmlContent
            };

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Verification email sent successfully via Brevo REST API to {Email}", toEmail);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email via Brevo REST API. StatusCode: {Status}, Response: {Error}", response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email via Brevo REST API to {Email}", toEmail);
        }
    }
}
