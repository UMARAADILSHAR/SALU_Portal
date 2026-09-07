using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Domain.Entities;

namespace SaluExamPortal.Application.Services;

public interface IAcademicLookupCacheService
{
    Task<IReadOnlyList<College>> GetCollegesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CollegeProgram>> GetCollegeProgramsAsync(Guid collegeId, CancellationToken cancellationToken = default);
    Task<AcademicYear?> GetActiveAcademicYearAsync(CancellationToken cancellationToken = default);
    void InvalidateAll();
}

public class AcademicLookupCacheService : IAcademicLookupCacheService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    private const string CollegesCacheKey = "SALU_CACHE_COLLEGES";
    private const string ActiveAcademicYearCacheKey = "SALU_CACHE_ACTIVE_ACADEMIC_YEAR";

    public AcademicLookupCacheService(IApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<College>> GetCollegesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CollegesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);

            return (IReadOnlyList<College>)await _dbContext.Colleges
                .AsNoTracking()
                .Include(c => c.Programs)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }) ?? Array.Empty<College>();
    }

    public async Task<IReadOnlyList<CollegeProgram>> GetCollegeProgramsAsync(Guid collegeId, CancellationToken cancellationToken = default)
    {
        var colleges = await GetCollegesAsync(cancellationToken);
        var college = colleges.FirstOrDefault(c => c.Id == collegeId);
        return college?.Programs.Where(p => p.IsActive).OrderBy(p => p.Name).ToList() ?? (IReadOnlyList<CollegeProgram>)Array.Empty<CollegeProgram>();
    }

    public async Task<AcademicYear?> GetActiveAcademicYearAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(ActiveAcademicYearCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);

            return await _dbContext.AcademicYears
                .AsNoTracking()
                .Include(a => a.EnrollmentWindows)
                .FirstOrDefaultAsync(a => a.IsActive, cancellationToken);
        });
    }

    public void InvalidateAll()
    {
        _cache.Remove(CollegesCacheKey);
        _cache.Remove(ActiveAcademicYearCacheKey);
    }
}
