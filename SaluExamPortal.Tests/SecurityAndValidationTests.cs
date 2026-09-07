using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using SaluExamPortal.Application.Services;

namespace SaluExamPortal.Tests;

public class SecurityAndValidationTests
{
    private readonly FileSecurityValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithAuthenticJpegBytes_ReturnsValid()
    {
        // Arrange: valid JPEG header (FF D8 FF E0 ...)
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(jpegBytes);

        // Act
        var result = await _validator.ValidateAsync(
            stream,
            "passport_photo.jpg",
            3 * 1024 * 1024,
            new[] { "image/jpeg", "image/png" });

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.DetectedMimeType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithAuthenticPdfBytes_ReturnsValid()
    {
        // Arrange: valid PDF header (%PDF-1.7)
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 standard document content for university enrollment");
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _validator.ValidateAsync(
            stream,
            "matric_marksheet.pdf",
            5 * 1024 * 1024,
            new[] { "image/jpeg", "image/png", "application/pdf" });

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.DetectedMimeType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithFakeExtensionExecutableBytes_RejectsFile()
    {
        // Arrange: Windows PE / EXE header (MZ ...) masquerading as a .jpg
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(exeBytes);

        // Act
        var result = await _validator.ValidateAsync(
            stream,
            "innocent_photo.jpg",
            3 * 1024 * 1024,
            new[] { "image/jpeg", "image/png" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.DetectedMimeType);
        Assert.Contains("Invalid file format", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithOversizedPayload_RejectsFile()
    {
        // Arrange: 10 bytes file with a 5 byte max limit
        var bytes = new byte[10];
        using var stream = new MemoryStream(bytes);

        // Act
        var result = await _validator.ValidateAsync(
            stream,
            "test.jpg",
            5,
            new[] { "image/jpeg" });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("exceeds the maximum allowed limit", result.ErrorMessage);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\Windows\\System32\\cmd.exe", "cmd.exe")]
    [InlineData("normal_student_doc.pdf", "normal_student_doc.pdf")]
    public void SanitizeFileName_StripsDirectoryTraversalPaths(string input, string expectedEnding)
    {
        // Act
        var clean = _validator.SanitizeFileName(input);

        // Assert
        Assert.DoesNotContain("..", clean);
        Assert.DoesNotContain('/', clean);
        Assert.DoesNotContain('\\', clean);
        Assert.EndsWith(expectedEnding, clean);
    }

    [Theory]
    [InlineData("4520612345671", "45206-1234567-1")]
    [InlineData("45206-1234567-1", "45206-1234567-1")]
    [InlineData("45206 1234567 1", "45206-1234567-1")]
    [InlineData("45206", "45206")]
    [InlineData("452061234", "45206-1234")]
    public void FormatCnic_ProducesExpectedFormat(string raw, string expected)
    {
        var digits = new string(raw.Where(char.IsDigit).Take(13).ToArray());
        var formatted = digits.Length switch
        {
            <= 5 => digits,
            <= 12 => $"{digits[..5]}-{digits[5..]}",
            _ => $"{digits[..5]}-{digits.Substring(5, 7)}-{digits[12]}"
        };
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("03001234567", "0300-1234567")]
    [InlineData("0300-1234567", "0300-1234567")]
    [InlineData("+923001234567", "0300-1234567")]
    [InlineData("923001234567", "0300-1234567")]
    [InlineData("0300", "0300")]
    public void FormatPhone_ProducesExpectedFormat(string raw, string expected)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("923") && digits.Length >= 12)
        {
            digits = "0" + digits[2..];
        }
        if (digits.Length > 11) digits = digits[..11];
        var formatted = digits.Length <= 4 ? digits : $"{digits[..4]}-{digits[4..]}";
        Assert.Equal(expected, formatted);
    }
}
