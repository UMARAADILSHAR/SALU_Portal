namespace SaluExamPortal.Application.Services;

public sealed record UploadedDocumentItem(
    string Id,
    string FileName,
    string RelativePath,
    string DocumentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    DocumentOcrResult? OcrResult = null,
    DocumentAuthenticityResult? Authenticity = null);

public interface IFileStorageService
{
    Task<string> SavePhotoAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default);
    Task<UploadedDocumentItem> SaveDocumentAsync(Stream stream, string originalFileName, string documentType, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string relativePath);
}

public sealed class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IFileSecurityValidator _securityValidator;
    private readonly ILogger<FileStorageService> _logger;

    private static readonly string[] AllowedPhotoMimes = ["image/jpeg", "image/png"];
    private static readonly string[] AllowedDocMimes = ["image/jpeg", "image/png", "application/pdf"];

    public FileStorageService(
        IWebHostEnvironment env,
        IFileSecurityValidator securityValidator,
        ILogger<FileStorageService> logger)
    {
        _env = env;
        _securityValidator = securityValidator;
        _logger = logger;
    }

    public async Task<string> SavePhotoAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default)
    {
        // 1. Buffer stream to memory for security validation
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        // 2. Strict Server-side Magic Byte & Size Validation (Max 3MB for portraits)
        var (isValid, errorMessage, detectedMimeType) = await _securityValidator.ValidateAsync(
            ms,
            originalFileName,
            3 * 1024 * 1024,
            AllowedPhotoMimes);

        if (!isValid)
        {
            _logger.LogWarning("Security validation rejected photo upload '{FileName}': {Error}", originalFileName, errorMessage);
            throw new InvalidOperationException(errorMessage ?? "Invalid photo file.");
        }

        var ext = detectedMimeType == "image/png" ? ".png" : ".jpg";
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "uploads");
        var photosDir = Path.Combine(uploadsRoot, "photos");
        Directory.CreateDirectory(photosDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(photosDir, safeFileName);

        ms.Position = 0;
        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await ms.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Security-verified photo saved to {Path}", fullPath);
        return $"/uploads/photos/{safeFileName}";
    }

    public async Task<UploadedDocumentItem> SaveDocumentAsync(Stream stream, string originalFileName, string documentType, CancellationToken cancellationToken = default)
    {
        // 1. Buffer stream to memory for security validation
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        // 2. Strict Server-side Magic Byte & Size Validation (Max 5MB for documents)
        var (isValid, errorMessage, detectedMimeType) = await _securityValidator.ValidateAsync(
            ms,
            originalFileName,
            5 * 1024 * 1024,
            AllowedDocMimes);

        if (!isValid)
        {
            _logger.LogWarning("Security validation rejected document upload '{FileName}': {Error}", originalFileName, errorMessage);
            throw new InvalidOperationException(errorMessage ?? "Invalid document file.");
        }

        var ext = detectedMimeType switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            _ => ".jpg"
        };

        var uploadsRoot = Path.Combine(_env.ContentRootPath, "uploads");
        var docsDir = Path.Combine(uploadsRoot, "documents");
        Directory.CreateDirectory(docsDir);

        var id = Guid.NewGuid().ToString("N");
        var safeFileName = $"{id}{ext}";
        var fullPath = Path.Combine(docsDir, safeFileName);

        ms.Position = 0;
        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await ms.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(fullPath);
        var item = new UploadedDocumentItem(
            Id: id,
            FileName: _securityValidator.SanitizeFileName(originalFileName),
            RelativePath: $"/uploads/documents/{safeFileName}",
            DocumentType: documentType,
            FileSizeBytes: fileInfo.Length,
            UploadedAt: DateTime.UtcNow
        );

        _logger.LogInformation("Security-verified document {DocType} ({OriginalName}) saved to {Path}", documentType, originalFileName, fullPath);
        return item;
    }

    public Task<bool> DeleteFileAsync(string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/"))
                return Task.FromResult(false);

            var webRoot = _env.ContentRootPath;
            var cleanRelative = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, cleanRelative);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {RelativePath}", relativePath);
        }

        return Task.FromResult(false);
    }
}
