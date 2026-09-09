using QRCoder;

namespace SaluExamPortal.Application.UniversityAdmission.Services;

public interface IQrCodeService
{
    byte[] GenerateQrCodePng(string text, int pixelsPerModule = 10);
    string GenerateQrCodeBase64(string text, int pixelsPerModule = 10);
}

public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrCodePng(string text, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(text))
            text = "SALU-VERIFY";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public string GenerateQrCodeBase64(string text, int pixelsPerModule = 10)
    {
        var bytes = GenerateQrCodePng(text, pixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
