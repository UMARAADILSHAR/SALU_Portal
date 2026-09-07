using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Application.Services;

public interface ICollegeScopingService
{
    Task<bool> IsGlobalAdminAsync(ClaimsPrincipal user);
    Task<Guid?> GetUserCollegeIdAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<bool> HasAccessToCollegeAsync(Guid? targetCollegeId, ClaimsPrincipal user, CancellationToken ct = default);
    Task EnsureAccessToCollegeAsync(Guid? targetCollegeId, ClaimsPrincipal user, CancellationToken ct = default);

    Task<IQueryable<Enrollment>> ApplyEnrollmentScopeAsync(IQueryable<Enrollment> query, ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<Fee>> ApplyFeeScopeAsync(IQueryable<Fee> query, ClaimsPrincipal user, CancellationToken ct = default);
    Task<IQueryable<AdmitCard>> ApplyAdmitCardScopeAsync(IQueryable<AdmitCard> query, ClaimsPrincipal user, CancellationToken ct = default);
}

public sealed class CollegeScopingService : ICollegeScopingService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;

    public CollegeScopingService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public Task<bool> IsGlobalAdminAsync(ClaimsPrincipal user)
    {
        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            return Task.FromResult(false);

        return Task.FromResult(user.IsInRole(nameof(PortalRole.SuperAdmin)) || user.IsInRole(nameof(PortalRole.Admin)));
    }

    public async Task<Guid?> GetUserCollegeIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var appUser = await _userManager.FindByIdAsync(userId);
        return appUser?.CollegeId;
    }

    public async Task<bool> HasAccessToCollegeAsync(Guid? targetCollegeId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(user)) return true;

        var userCollegeId = await GetUserCollegeIdAsync(user, ct);
        if (!userCollegeId.HasValue) return false;

        return targetCollegeId.HasValue && userCollegeId.Value == targetCollegeId.Value;
    }

    public async Task EnsureAccessToCollegeAsync(Guid? targetCollegeId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!await HasAccessToCollegeAsync(targetCollegeId, user, ct))
        {
            throw new UnauthorizedAccessException("Access denied: You do not have permission to access or modify records for this institution.");
        }
    }

    public async Task<IQueryable<Enrollment>> ApplyEnrollmentScopeAsync(IQueryable<Enrollment> query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(user)) return query;

        if (user.IsInRole(nameof(PortalRole.CollegeAdmin)))
        {
            var collegeId = await GetUserCollegeIdAsync(user, ct);
            if (collegeId.HasValue)
            {
                return query.Where(e => e.CollegeId == collegeId.Value);
            }
            return query.Where(e => false); // Block if unassigned
        }

        if (user.IsInRole(nameof(PortalRole.Student)))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return query.Where(e => e.UserId == userId);
        }

        return query.Where(e => false);
    }

    public async Task<IQueryable<Fee>> ApplyFeeScopeAsync(IQueryable<Fee> query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(user)) return query;

        if (user.IsInRole(nameof(PortalRole.CollegeAdmin)))
        {
            var collegeId = await GetUserCollegeIdAsync(user, ct);
            if (collegeId.HasValue)
            {
                return query.Where(f => f.Enrollment.CollegeId == collegeId.Value);
            }
            return query.Where(f => false);
        }

        if (user.IsInRole(nameof(PortalRole.Student)))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return query.Where(f => f.Enrollment.UserId == userId);
        }

        return query.Where(f => false);
    }

    public async Task<IQueryable<AdmitCard>> ApplyAdmitCardScopeAsync(IQueryable<AdmitCard> query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(user)) return query;

        if (user.IsInRole(nameof(PortalRole.CollegeAdmin)))
        {
            var collegeId = await GetUserCollegeIdAsync(user, ct);
            if (collegeId.HasValue)
            {
                return query.Where(a => a.Enrollment.CollegeId == collegeId.Value);
            }
            return query.Where(a => false);
        }

        if (user.IsInRole(nameof(PortalRole.Student)))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return query.Where(a => a.Enrollment.UserId == userId);
        }

        return query.Where(a => false);
    }
}
