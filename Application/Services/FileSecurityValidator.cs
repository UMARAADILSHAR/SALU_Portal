using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SaluExamPortal.Application.Services;

public interface IFileSecurityValidator
{
    Task<(bool IsValid, string? ErrorMessage, string? DetectedMimeType)> ValidateAsync(
        Stream fileStream,
        string originalFileName,
        long maxSizeBytes,
        string[] allowedMimeTypes);

    string SanitizeFileName(string fileName);
}

public class FileSecurityValidator : IFileSecurityValidator
{
    private static readonly Dictionary<string, List<byte[]>> FileSignatures = new()
    {
        { "image/jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
        { "image/png",  new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { "application/pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } } } // %PDF
    };

    public async Task<(bool IsValid, string? ErrorMessage, string? DetectedMimeType)> ValidateAsync(
        Stream fileStream,
        string originalFileName,
        long maxSizeBytes,
        string[] allowedMimeTypes)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            return (false, "File is empty or missing.", null);
        }

        if (fileStream.Length > maxSizeBytes)
        {
            var maxMb = maxSizeBytes / (1024.0 * 1024.0);
            return (false, $"File size ({fileStream.Length / (1024.0 * 1024.0):F1} MB) exceeds the maximum allowed limit of {maxMb:F1} MB.", null);
        }

        // Reset stream position to read header
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        // Read the first 16 bytes for magic-number inspection
        var headerBytes = new byte[16];
        var bytesRead = await fileStream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length));

        // Restore stream position
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        if (bytesRead < 4)
        {
            return (false, "Corrupt or invalid file header.", null);
        }

        // Detect real MIME type based on binary signature
        string? detectedMimeType = null;
        foreach (var (mimeType, signatures) in FileSignatures)
        {
            foreach (var signature in signatures)
            {
                if (headerBytes.Take(signature.Length).SequenceEqual(signature))
                {
                    detectedMimeType = mimeType;
                    break;
                }
            }
            if (detectedMimeType != null) break;
        }

        if (detectedMimeType == null)
        {
            return (false, "Invalid file format. Only authentic JPG, PNG, and PDF files are allowed.", null);
        }

        if (!allowedMimeTypes.Contains(detectedMimeType, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Detected file type ({detectedMimeType}) is not permitted for this upload.", detectedMimeType);
        }

        return (true, null, detectedMimeType);
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Guid.NewGuid().ToString("N");
        }

        // Remove path components to prevent path traversal
        var baseName = Path.GetFileName(fileName);
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanName = new string(baseName.Where(c => !invalidChars.Contains(c)).ToArray());

        return string.IsNullOrWhiteSpace(cleanName) ? Guid.NewGuid().ToString("N") : cleanName;
    }
}
