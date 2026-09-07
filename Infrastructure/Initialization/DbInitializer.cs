using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;
using SaluExamPortal.Infrastructure.Persistence;

namespace SaluExamPortal.Infrastructure.Initialization;

public static class DbInitializer
{
    private static ILogger? _logger;

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        _logger = services.GetRequiredService<ILoggerFactory>()
                          .CreateLogger("SaluExamPortal.DbInitializer");
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Ensure columns exist on AspNetUsers defensively in case of schema discrepancy
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'TotpEnabled')
            BEGIN
                ALTER TABLE AspNetUsers ADD TotpEnabled bit NOT NULL CONSTRAINT DF_AspNetUsers_TotpEnabled DEFAULT 0;
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'TotpEnabledAt')
            BEGIN
                ALTER TABLE AspNetUsers ADD TotpEnabledAt datetime2 NULL;
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'TotpSecretEncrypted')
            BEGIN
                ALTER TABLE AspNetUsers ADD TotpSecretEncrypted nvarchar(500) NULL;
            END;
        ");

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Enum.GetNames<PortalRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@salu.edu.pk";
        var adminPassword = configuration["SeedAdmin:Password"];
        var admin = await userManager.FindByEmailAsync(adminEmail)
                    ?? await userManager.Users.FirstOrDefaultAsync(u => u.Cnic == "42101-0000000-1");
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                _logger.LogWarning("No SeedAdmin password is configured. Skipping initial administrator creation; configure SeedAdmin:Password through user secrets or environment configuration to create one.");
            }
            else
            {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "University Super Administrator",
                FatherName = "System",
                Cnic = "42101-0000000-1",
                IsVerified = true,
                MustChangePassword = false
            };
            
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create admin user: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            
            _logger.LogInformation("Created new admin user: {Email}", adminEmail);
            }
        }
        else
        {
            // Never reset an existing administrator password during application startup.
            // The configured password is used only when creating the initial account.
            _logger.LogInformation("Existing administrator retained; startup password synchronization is disabled for: {Email}", adminEmail);
        }

        if (admin is not null && !await userManager.IsInRoleAsync(admin, nameof(PortalRole.SuperAdmin)))
            await userManager.AddToRoleAsync(admin, nameof(PortalRole.SuperAdmin));

        foreach (var existingUser in await userManager.Users.ToListAsync())
        {
            if ((admin is not null && existingUser.Id == admin.Id) || (await userManager.GetRolesAsync(existingUser)).Count > 0)
                continue;

            await userManager.AddToRoleAsync(existingUser, nameof(PortalRole.Student));
        }

        if (!await db.AcademicYears.AnyAsync())
        {
            var year = new AcademicYear
            {
                Name = $"{DateTime.UtcNow.Year}-{DateTime.UtcNow.Year + 1}",
                StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1),
                EndDate = new DateTime(DateTime.UtcNow.Year + 1, 12, 31),
                IsActive = true
            };
            db.AcademicYears.Add(year);
            db.EnrollmentWindows.Add(new EnrollmentWindow
            {
                AcademicYear = year,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(6),
                IsOpen = true
            });
        }

        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.AddRange(
                new SystemSetting { Key = "enrollment_fee_amount", Value = "3840.00" },
                new SystemSetting { Key = "migration_noc_fee_amount", Value = "1000.00" },
                new SystemSetting { Key = "challan_validity_days", Value = "7" },
                new SystemSetting { Key = "allow_enrollment", Value = "true" },
                new SystemSetting { Key = "site_name", Value = "Shah Abdul Latif University Exam Portal" },
                new SystemSetting { Key = "site_email", Value = "info@saluexamportal.edu.pk" },
                new SystemSetting { Key = "site_phone", Value = "0243-9280051" });
        }

        if (!await db.Colleges.AnyAsync())
        {
            foreach (var item in CollegeCatalog)
            {
                var college = new College
                {
                    Name = item.Name,
                    Code = item.Code,
                    District = item.District,
                    Type = item.Type,
                    PrincipalName = item.PrincipalName,
                    Programs = item.Programs.Select(program => new CollegeProgram
                    {
                        Name = program.Name,
                        Code = program.Code
                    }).ToList()
                };
                db.Colleges.Add(college);
            }
        }

        await db.SaveChangesAsync();
    }

    private static readonly CollegeSeed[] CollegeCatalog =
    [
        new("GSSC-KHP", "Govt: Superior Science College Khairpur", "Khairpur", CollegeType.Boys, [new("Associate Degree in Science", "ADS")]),
        new("MDC-KHP", "Govt: Mumtaz Degree College Khairpur", "Khairpur", CollegeType.Coed, [new("Associate Degree in Commerce", "ADC"), new("Associate Degree in Arts", "ADA")]),
        new("GDW-KHP", "Govt: Degree College for Women Khairpur", "Khairpur", CollegeType.Girls, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA"), new("Associate Degree in Commerce", "ADC")]),
        new("DSC-GAM", "Govt: Degree Science College Gambat", "Khairpur", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("BS Computer Science", "BS-CS")]),
        new("BPGC-SUK", "Govt: Boys Postgraduate Degree College Sukkur", "Sukkur", CollegeType.Boys, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA"), new("Master of Arts", "MA")]),
        new("GGC-SUK", "Govt: Girls Degree College Sukkur", "Sukkur", CollegeType.Girls, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Commerce", "ADC"), new("BS Information Technology", "BS-IT"), new("Bachelor of Business Administration", "BBA")]),
        new("ISC-SUK", "Govt: Islamia Science College Sukkur", "Sukkur", CollegeType.Boys, [new("Associate Degree in Science", "ADS"), new("BS Computer Science", "BS-CS")]),
        new("DGC-GHT", "Govt: Degree College Ghotki", "Ghotki", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA")]),
        new("DGC-SKP", "Govt: C & S Degree College Shikarpur", "Shikarpur", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA")]),
        new("DGC-LRK", "Govt: Degree College Larkana", "Larkana", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA"), new("Associate Degree in Commerce", "ADC")]),
        new("DGC-JCB", "Govt: Degree College Jacobabad", "Jacobabad", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA")]),
        new("DGC-DAD", "Govt: Degree College Dadu", "Dadu", CollegeType.Coed, [new("Associate Degree in Science", "ADS"), new("Associate Degree in Arts", "ADA")])
    ];

    private sealed record CollegeSeed(string Code, string Name, string District, CollegeType Type, ProgramSeed[] Programs, string? PrincipalName = null);
    private sealed record ProgramSeed(string Name, string Code);
}
