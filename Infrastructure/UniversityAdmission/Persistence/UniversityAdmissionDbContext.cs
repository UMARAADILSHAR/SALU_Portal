using Microsoft.EntityFrameworkCore;

namespace SaluExamPortal.Infrastructure.UniversityAdmission.Persistence;

public sealed class UniversityAdmissionDbContext(DbContextOptions<UniversityAdmissionDbContext> options) : DbContext(options)
{
    public DbSet<UniversityAdmissionApplication> Applications => Set<UniversityAdmissionApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UniversityAdmissionApplication>(entity =>
        {
            entity.ToTable("UniversityAdmissionApplications");
            entity.HasKey(application => application.Id);
            entity.Property(application => application.UserId).HasMaxLength(450).IsRequired();
            entity.Property(application => application.ApplicationNumber).HasMaxLength(40).IsRequired();
            entity.Property(application => application.Status).HasMaxLength(30).IsRequired();
            entity.Property(application => application.FormStateJson).IsRequired();
            entity.HasIndex(application => application.UserId).IsUnique();
            entity.HasIndex(application => application.ApplicationNumber).IsUnique();
        });
    }
}
