using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public interface IDualControlApprovalService
{
    Task<Guid> SubmitChangeRequestAsync(
        string requestType,
        string targetEntityName,
        string targetEntityId,
        object proposedPayload,
        string justification,
        string makerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ApproveChangeRequestAsync(
        Guid requestId,
        string checkerUserId,
        string totpCode,
        CancellationToken cancellationToken = default);

    Task<bool> RejectChangeRequestAsync(
        Guid requestId,
        string checkerUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default);
}

public sealed class DualControlApprovalService : IDualControlApprovalService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITotpService _totpService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DualControlApprovalService> _logger;

    public DualControlApprovalService(
        IApplicationDbContext dbContext,
        ITotpService totpService,
        UserManager<ApplicationUser> userManager,
        ILogger<DualControlApprovalService> logger)
    {
        _dbContext = dbContext;
        _totpService = totpService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Guid> SubmitChangeRequestAsync(
        string requestType,
        string targetEntityName,
        string targetEntityId,
        object proposedPayload,
        string justification,
        string makerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker identity is mandatory.", nameof(makerUserId));

        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Justification reason is required for dual-authorization proposals.", nameof(justification));

        var request = new MakerCheckerRequest
        {
            RequestType = requestType,
            TargetEntityName = targetEntityName,
            TargetEntityId = targetEntityId,
            ProposedPayloadJson = JsonSerializer.Serialize(proposedPayload),
            Justification = justification,
            MakerUserId = makerUserId,
            MakerSubmittedAt = DateTime.UtcNow,
            ReviewStatus = "Pending"
        };

        _dbContext.MakerCheckerRequests.Add(request);

        // Also add an immutable audit trail entry
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = makerUserId,
            Action = $"PROPOSE_CHANGE_{requestType.ToUpperInvariant()}",
            Entity = targetEntityName,
            EntityId = targetEntityId,
            Details = $"Maker submitted change request {request.Id}: {justification}"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maker {MakerId} submitted change proposal {RequestId} for entity {Entity} ({EntityId})",
            makerUserId, request.Id, targetEntityName, targetEntityId);

        return request.Id;
    }

    public async Task<bool> ApproveChangeRequestAsync(
        Guid requestId,
        string checkerUserId,
        string totpCode,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.MakerCheckerRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
            throw new KeyNotFoundException($"Change request {requestId} does not exist.");

        if (request.ReviewStatus != "Pending")
            throw new InvalidOperationException($"Request {requestId} is already {request.ReviewStatus}.");

        // ===== SEGREGATION OF DUTIES ENFORCEMENT =====
        // Hard Institutional Rule #1: Maker cannot approve their own change
        if (string.Equals(request.MakerUserId, checkerUserId, StringComparison.Ordinal))
        {
            _logger.LogCritical(
                "[SECURITY VIOLATION]: Same user '{UserId}' attempted dual-control self-approval for {RequestId}!",
                checkerUserId, requestId);
            throw new InvalidOperationException(
                "Security Policy Violation: The Checker MUST be a different authorized official than the Maker.");
        }

        // Verify Checker exists and has proper authorization role
        var checker = await _userManager.FindByIdAsync(checkerUserId);
        if (checker == null)
            throw new KeyNotFoundException($"Checker user '{checkerUserId}' not found.");

        // Hard Institutional Rule #2: Checker must have explicit approval authority
        // (Role-based authorization should be verified at controller level,
        // but we add belt-and-suspenders check here)
        if (!checker.IsVerified)
        {
            _logger.LogWarning("Unverified checker '{CheckerId}' attempted approval", checkerUserId);
            throw new UnauthorizedAccessException("Checker account not verified for dual-control operations.");
        }

        // Hard Institutional Rule #3: Approval must occur within 24 hours
        var elapsedTime = DateTime.UtcNow - request.MakerSubmittedAt;
        if (elapsedTime > TimeSpan.FromHours(24))
        {
            _logger.LogWarning(
                "Change request {RequestId} approval attempted after expiry ({Hours} hours)",
                requestId, elapsedTime.TotalHours);
            throw new InvalidOperationException(
                "Change request has expired. Requests must be approved within 24 hours of submission.");
        }

        // ===== RFC 6238 TOTP VERIFICATION =====
        if (string.IsNullOrWhiteSpace(checker.TotpSecretEncrypted))
        {
            _logger.LogWarning(
                "[SECURITY]: Checker '{CheckerId}' does not have 2FA enabled for dual-control",
                checkerUserId);
            throw new InvalidOperationException(
                "Checker account requires 2FA (TOTP) enrollment for dual-control approval.");
        }

        // Decrypt and validate TOTP
        string decryptedSecret;
        try
        {
            decryptedSecret = _totpService.DecryptTotpSecret(checker.TotpSecretEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt TOTP secret for checker '{CheckerId}'", checkerUserId);
            throw new InvalidOperationException("TOTP configuration error. Contact support.");
        }

        if (!_totpService.ValidateTotpCode(decryptedSecret, totpCode))
        {
            _logger.LogWarning(
                "[AUTH ATTEMPT]: Checker '{CheckerId}' failed TOTP verification for proposal {RequestId}",
                checkerUserId, requestId);
            
            // Increment failed attempt counter (rate limiting)
            // TODO: Implement account lockout after 3 failed attempts
            
            throw new UnauthorizedAccessException(
                "Invalid or expired 2FA TOTP code. Please verify the code is correct and try again.");
        }

        // ===== APPROVAL STATE TRANSITION =====
        request.CheckerUserId = checkerUserId;
        request.CheckerReviewedAt = DateTime.UtcNow;
        request.ReviewStatus = "Approved";
        request.TotpVerificationRef = $"RFC6238-VERIFIED-{DateTime.UtcNow:yyyyMMddHHmmssff}";

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = checkerUserId,
            Action = $"APPROVE_CHANGE_{request.RequestType.ToUpperInvariant()}",
            Entity = request.TargetEntityName,
            EntityId = request.TargetEntityId,
            Details = $"Checker {checkerUserId} approved proposal {request.Id} with RFC 6238 TOTP verification."
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[APPROVAL]: Checker '{CheckerId}' successfully approved proposal {RequestId} for {Entity}",
            checkerUserId, requestId, request.TargetEntityName);
        
        return true;
    }

    public async Task<bool> RejectChangeRequestAsync(
        Guid requestId,
        string checkerUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.MakerCheckerRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
            throw new KeyNotFoundException($"Change request {requestId} does not exist.");

        if (string.Equals(request.MakerUserId, checkerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Dual Authorization Violation: The Checker must be different from the Maker.");
        }

        request.CheckerUserId = checkerUserId;
        request.CheckerReviewedAt = DateTime.UtcNow;
        request.ReviewStatus = "Rejected";
        request.RejectionReason = rejectionReason;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = checkerUserId,
            Action = $"REJECT_CHANGE_{request.RequestType.ToUpperInvariant()}",
            Entity = request.TargetEntityName,
            EntityId = request.TargetEntityId,
            Details = $"Checker {checkerUserId} rejected proposal {request.Id}. Reason: {rejectionReason}"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
