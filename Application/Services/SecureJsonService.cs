using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaluExamPortal.Application.Services;

/// <summary>
/// Secure JSON deserialization with size limits and schema validation.
/// Prevents DoS attacks via large JSON blobs and malformed data.
/// </summary>
public interface ISecureJsonService
{
    /// <summary>
    /// Safely deserialize JSON with size validation and schema checks.
    /// </summary>
    T? DeserializeWithValidation<T>(string? json, int maxSizeBytes = 10240) where T : class;

    /// <summary>
    /// Validate JSON string size against limit.
    /// </summary>
    bool ValidateJsonSize(string? json, int maxSizeBytes);
}

public sealed class SecureJsonService : ISecureJsonService
{
    private readonly ILogger<SecureJsonService> _logger;

    // Default JSON serializer options with security settings
    private static readonly JsonSerializerOptions SecureJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 10, // Prevent stack overflow from deeply nested objects
        IgnoreReadOnlyProperties = true,
        IncludeFields = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false // Minimize response size
    };

    public SecureJsonService(ILogger<SecureJsonService> logger)
    {
        _logger = logger;
    }

    public T? DeserializeWithValidation<T>(string? json, int maxSizeBytes = 10240) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            // Validate size to prevent DoS
            if (!ValidateJsonSize(json, maxSizeBytes))
            {
                _logger.LogWarning(
                    "JSON deserialization rejected: size {Size} exceeds limit {MaxSize}",
                    json.Length, maxSizeBytes);
                throw new InvalidOperationException(
                    $"JSON payload exceeds maximum allowed size of {maxSizeBytes} bytes");
            }

            // Attempt deserialization with security options
            var result = JsonSerializer.Deserialize<T>(json, SecureJsonOptions);
            
            if (result == null)
            {
                _logger.LogWarning("JSON deserialization returned null for type '{Type}'", typeof(T).Name);
                return null;
            }

            // Validate deserialized object is not malicious
            ValidateDeserializedObject(result);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for type '{Type}'", typeof(T).Name);
            throw new InvalidOperationException("Invalid JSON format", ex);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "JSON deserialization not supported for type '{Type}'", typeof(T).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deserializing JSON for type '{Type}'", typeof(T).Name);
            throw;
        }
    }

    public bool ValidateJsonSize(string? json, int maxSizeBytes)
    {
        if (string.IsNullOrEmpty(json))
            return true;

        // Check byte size (UTF-8 encoding)
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(json);
        return byteCount <= maxSizeBytes;
    }

    private static void ValidateDeserializedObject<T>(T? obj) where T : class
    {
        if (obj == null)
            return;

        // Check for suspicious patterns in string properties
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.PropertyType != typeof(string))
                continue;

            var value = prop.GetValue(obj) as string;
            if (string.IsNullOrEmpty(value))
                continue;

            // Detect potential injection attacks
            if (ContainsSuspiciousPatterns(value))
            {
                throw new InvalidOperationException(
                    $"Suspicious content detected in property '{prop.Name}'");
            }
        }
    }

    private static bool ContainsSuspiciousPatterns(string value)
    {
        // Check for SQL injection patterns
        var sqlPatterns = new[] { "'; DROP", "UNION SELECT", "--", ";DELETE", "xp_" };
        foreach (var pattern in sqlPatterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check for script injection
        var scriptPatterns = new[] { "<script", "javascript:", "onerror=", "onclick=" };
        foreach (var pattern in scriptPatterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
