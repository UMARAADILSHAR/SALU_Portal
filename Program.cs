using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using MudBlazor.Services;
using SaluExamPortal.Components;
using SaluExamPortal.Components.Features.Authentication;
using SaluExamPortal.Infrastructure.Persistence;
using SaluExamPortal.Infrastructure.Initialization;
using SaluExamPortal.Application.Common.Interfaces;
using SaluExamPortal.Application.Services;
using SaluExamPortal.Domain.Enums;
using SaluExamPortal.Infrastructure.UniversityAdmission.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(); // Add controller support for API endpoints
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 15 * 1024 * 1024; // 15MB for photo and document uploads
});


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Core Domain & Application Services
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddSingleton<IFileSecurityValidator, FileSecurityValidator>();
builder.Services.AddScoped<IAcademicLookupCacheService, AcademicLookupCacheService>();
// RFC 6238 TOTP 2FA Service
builder.Services.AddScoped<ITotpService, TotpService>();
// Data Protection for PII encryption
builder.Services.AddDataProtection();
// Secure JSON deserialization
builder.Services.AddScoped<ISecureJsonService, SecureJsonService>();
// Enrollment input validation
builder.Services.AddScoped<IEnrollmentValidationService, EnrollmentValidationService>();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3500;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
});
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<ICollegeScopingService, CollegeScopingService>();
builder.Services.AddScoped<IFeeCalculationService, FeeCalculationService>();
builder.Services.AddScoped<IFeePaymentService, FeePaymentService>();
builder.Services.AddScoped<IRollNumberService, RollNumberService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IEnrollmentCorrectionService, EnrollmentCorrectionService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IDocumentOcrService, TesseractOcrService>();
builder.Services.AddScoped<IDocumentAuthenticityService, DocumentAuthenticityService>();
builder.Services.AddHostedService<EnrollmentDocumentOcrWorker>();
builder.Services.AddScoped<IExamAdmitCardEngineService, ExamAdmitCardEngineService>();
builder.Services.AddScoped<IDualControlApprovalService, DualControlApprovalService>();
builder.Services.AddHostedService<ExamIntegrityReconciliationWorker>();
// Toast notification service (scoped = per SignalR circuit)
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<SaluExamPortal.Application.UniversityAdmission.Services.AdmissionFormService>();
builder.Services.AddScoped<SaluExamPortal.Application.UniversityAdmission.Services.SetupDataService>();
builder.Services.AddScoped<SaluExamPortal.Application.UniversityAdmission.Services.UniversityAdmissionApplicationStore>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UniversityAdministration", policy => policy.RequireRole(nameof(PortalRole.SuperAdmin), nameof(PortalRole.Admin)));
    options.AddPolicy("CollegeAdministration", policy => policy.RequireRole(nameof(PortalRole.SuperAdmin), nameof(PortalRole.Admin), nameof(PortalRole.CollegeAdmin)));
    options.AddPolicy("Student", policy => policy.RequireRole(nameof(PortalRole.Student)));
});

var universityAdmissionConnection = builder.Configuration.GetConnectionString("UniversityAdmissionConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<UniversityAdmissionDbContext>(options =>
{
    options.UseSqlServer(universityAdmissionConnection, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
    });
});

builder.Services.AddRateLimiter(options =>
{
    // Rate limit for authentication endpoints (brute force protection)
    options.AddPolicy("auth-limiter", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Rate limit for file uploads (DoS protection)
    options.AddPolicy("upload-limiter", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (context.Request.Path.StartsWithSegments("/Account"))
            return RateLimitPartition.GetFixedWindowLimiter(
                $"auth_{ip}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        
        if (context.Request.Path.StartsWithSegments("/api/uploads"))
            return RateLimitPartition.GetFixedWindowLimiter(
                $"upload_{ip}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0
                });

        return RateLimitPartition.GetNoLimiter("unlimited");
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Resilient SQL Server Connection with Exponential Backoff Retry & Automated Auditing Interceptor
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
});

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddHealthChecks();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        // Enforce strong passwords for institutional compliance
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false; // Special characters optional
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        // Account lockout after failed attempts (brute force protection)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider, builder.Configuration);
    try
    {
        await scope.ServiceProvider.GetRequiredService<UniversityAdmissionDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var migrationLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("UniversityAdmissionMigration");
        migrationLogger.LogError(ex, "An error occurred while migrating the University Admission database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Enterprise Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRateLimiter();

// Keep uploaded documents outside the public static-file pipeline. They are served
// only through the authorization-aware DownloadsController.
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

var photosPath = Path.Combine(uploadsPath, "photos");
if (!Directory.Exists(photosPath))
{
    Directory.CreateDirectory(photosPath);
}

var docsPath = Path.Combine(uploadsPath, "documents");
if (!Directory.Exists(docsPath))
{
    Directory.CreateDirectory(docsPath);
}

app.UseStaticFiles(); // Serves wwwroot

// Serve student photos and document previews from uploads directory
var fileExtensionProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
fileExtensionProvider.Mappings[".webp"] = "image/webp";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = fileExtensionProvider
});


app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapControllers(); // Map API controller routes
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
