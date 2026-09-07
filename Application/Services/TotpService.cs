using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace SaluExamPortal.Application.Services;

public interface ITotpService
{
    (string Base32Secret, byte[] QRCodeBytes) GenerateTotpSecret(string userEmail);
    bool ValidateTotpCode(string totpSecret, string userProvidedCode);
    string EncryptTotpSecret(string plainSecret);
    string DecryptTotpSecret(string encryptedSecret);
    string[] GenerateRecoveryCodes();
}

public sealed class TotpService : ITotpService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<TotpService> _logger;
    private const int TotpStep = 30;
    private const int TotpDigits = 6;
    private const int RecoveryCodeCount = 10;

    public TotpService(IDataProtectionProvider dataProtectionProvider, ILogger<TotpService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector("SaluExamPortal.TotpService");
        _logger = logger;
    }

    public (string Base32Secret, byte[] QRCodeBytes) GenerateTotpSecret(string userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            throw new ArgumentException("User email is required", nameof(userEmail));
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var base32Secret = ToBase32(secretBytes);
        var totpUri = $"otpauth://totp/{Uri.EscapeDataString("SaluExamPortal")}:{Uri.EscapeDataString(userEmail)}" +
                      $"?secret={base32Secret}&issuer={Uri.EscapeDataString("SaluExamPortal")}&period={TotpStep}";
        var qrCodeBytes = GenerateQRCode(totpUri);
        _logger.LogInformation("Generated TOTP secret for user {UserEmail}", userEmail);
        return (base32Secret, qrCodeBytes);
    }

    public bool ValidateTotpCode(string totpSecret, string userProvidedCode)
    {
        if (string.IsNullOrWhiteSpace(totpSecret) || string.IsNullOrWhiteSpace(userProvidedCode)) return false;
        if (userProvidedCode.Length != TotpDigits || !userProvidedCode.All(char.IsDigit)) return false;
        try
        {
            var secretBytes = FromBase32(totpSecret);
            var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TotpStep;
            for (long w = -1; w <= 1; w++)
            {
                if (GenerateTotp(secretBytes, counter + w) == userProvidedCode)
                {
                    _logger.LogInformation("TOTP validated at window {W}", w);
                    return true;
                }
            }
            _logger.LogWarning("TOTP validation failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TOTP validation error");
            return false;
        }
    }

    public string EncryptTotpSecret(string plainSecret)
    {
        if (string.IsNullOrWhiteSpace(plainSecret)) throw new ArgumentException("Secret cannot be empty", nameof(plainSecret));
        return _protector.Protect(plainSecret);
    }

    public string DecryptTotpSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret)) throw new ArgumentException("Encrypted secret cannot be empty", nameof(encryptedSecret));
        return _protector.Unprotect(encryptedSecret);
    }

    public string[] GenerateRecoveryCodes()
    {
        var codes = new string[RecoveryCodeCount];
        for (int i = 0; i < RecoveryCodeCount; i++)
        {
            var b = RandomNumberGenerator.GetBytes(4);
            codes[i] = (BitConverter.ToUInt32(b, 0) % 100_000_000).ToString("D8");
        }
        return codes;
    }

    private static string GenerateTotp(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);
        return (binary % (int)Math.Pow(10, TotpDigits)).ToString().PadLeft(TotpDigits, '0');
    }

    private static readonly char[] Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    private static string ToBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0) sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] FromBase32(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (char c in input)
        {
            int val = Array.IndexOf(Base32Chars, c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)(buffer >> bitsLeft));
            }
        }
        return output.ToArray();
    }

    private static byte[] GenerateQRCode(string totpUri)
    {
        try
        {
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(10);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"QR code generation failed: {ex.Message}");
            return [];
        }
    }
}
