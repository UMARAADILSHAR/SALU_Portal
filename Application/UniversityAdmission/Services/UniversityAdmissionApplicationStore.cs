using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaluExamPortal.Infrastructure.UniversityAdmission.Persistence;

namespace SaluExamPortal.Application.UniversityAdmission.Services;

public sealed class UniversityAdmissionApplicationStore(UniversityAdmissionDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UniversityAdmissionApplication> SaveDraftAsync(string userId, object formState, CancellationToken cancellationToken = default)
    {
        var application = await db.Applications.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var now = DateTime.UtcNow;

        if (application is null)
        {
            application = new UniversityAdmissionApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationNumber = $"UR-{now:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}",
                CreatedAtUtc = now
            };
            db.Applications.Add(application);
        }

        application.FormStateJson = JsonSerializer.Serialize(formState, JsonOptions);
        application.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<UniversityAdmissionApplication?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await db.Applications.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public async Task<UniversityAdmissionApplication?> GetByApplicationNumberAsync(string appNumber, CancellationToken cancellationToken = default)
    {
        return await db.Applications.SingleOrDefaultAsync(item => item.ApplicationNumber == appNumber, cancellationToken);
    }
}
