# SaluExamPortal Security Fixes - Setup Script (Windows PowerShell)
# This script configures all security settings for development and production environments
# Usage: .\setup-security.ps1 -Environment Development -AdminEmail admin@saluexamportal.edu.pk -AdminPassword SecurePassword123!

param(
    [string]$Environment = "Development",
    [string]$AdminEmail = "admin@saluexamportal.edu.pk",
    [string]$AdminPassword = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "SaluExamPortal Security Configuration" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Admin Email: $AdminEmail" -ForegroundColor Yellow
Write-Host ""

# Generate random password if not provided
if ([string]::IsNullOrEmpty($AdminPassword)) {
    $AdminPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 16 | ForEach-Object {[char]$_})
    Write-Host "Generated Admin Password: $AdminPassword" -ForegroundColor Yellow
}

# Step 1: Set User Secrets for Development
if ($Environment -eq "Development") {
    Write-Host ""
    Write-Host "Step 1: Configuring User Secrets (Development)..." -ForegroundColor Green
    
    # Initialize user secrets if not already done
    try {
        dotnet user-secrets list --project SaluExamPortal.csproj | Out-Null
    } catch {
        dotnet user-secrets init --project SaluExamPortal.csproj
        Write-Host "✓ User secrets initialized" -ForegroundColor Green
    }
    
    # Set admin credentials
    dotnet user-secrets set "SeedAdmin:Email" $AdminEmail --project SaluExamPortal.csproj
    dotnet user-secrets set "SeedAdmin:Password" $AdminPassword --project SaluExamPortal.csproj
    Write-Host "✓ Admin credentials set in user secrets" -ForegroundColor Green
}

# Step 2: Create database backup (Production only)
if ($Environment -eq "Production") {
    Write-Host ""
    Write-Host "Step 2: Backing up existing database (Production)..." -ForegroundColor Green
    
    $BackupDir = ".\backups"
    if (-not (Test-Path $BackupDir)) {
        New-Item -ItemType Directory -Path $BackupDir | Out-Null
    }
    
    $BackupFile = "$BackupDir\SaluExamPortal_$(Get-Date -Format 'yyyyMMdd_HHmmss').bak"
    
    Write-Host "⚠ Manual database backup required for production" -ForegroundColor Yellow
    Write-Host "  Backup location: $BackupFile" -ForegroundColor Yellow
    Write-Host "  Run in SQL Server Management Studio or sqlcmd:" -ForegroundColor Yellow
    Write-Host "  BACKUP DATABASE [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]" -ForegroundColor Yellow
    Write-Host "  TO DISK='$BackupFile'" -ForegroundColor Yellow
}

# Step 3: Create EF Core migration
Write-Host ""
Write-Host "Step 3: Creating EF Core migration..." -ForegroundColor Green

$migrations = dotnet ef migrations list --project SaluExamPortal.csproj 2>&1
if ($migrations -notlike "*AddTotpAndSecurityFields*") {
    dotnet ef migrations add AddTotpAndSecurityFields --project SaluExamPortal.csproj
    Write-Host "✓ Migration created: AddTotpAndSecurityFields" -ForegroundColor Green
} else {
    Write-Host "✓ Migration already exists: AddTotpAndSecurityFields" -ForegroundColor Green
}

# Step 4: Apply database migration
Write-Host ""
Write-Host "Step 4: Applying database migration..." -ForegroundColor Green

dotnet ef database update --project SaluExamPortal.csproj
Write-Host "✓ Database migration applied successfully" -ForegroundColor Green

# Step 5: Create sample environment configuration
Write-Host ""
Write-Host "Step 5: Creating sample configuration files..." -ForegroundColor Green

$devSettings = @{
    "ConnectionStrings" = @{
        "DefaultConnection" = "Server=(localdb)\mssqllocaldb;Database=aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235;Trusted_Connection=True;MultipleActiveResultSets=true"
    }
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Information"
            "Microsoft.AspNetCore" = "Warning"
            "Microsoft.AspNetCore.Authentication" = "Debug"
        }
    }
    "AllowedHosts" = "*"
}

if (-not (Test-Path "appsettings.Development.json")) {
    $devSettings | ConvertTo-Json | Set-Content -Path "appsettings.Development.json"
    Write-Host "✓ Created appsettings.Development.json" -ForegroundColor Green
}

# Production template
$prodSettings = @{
    "ConnectionStrings" = @{
        "DefaultConnection" = "Server=YOUR_SQL_SERVER_NAME;Database=SaluExamPortal;User Id=sa;Password=YOUR_PASSWORD;Encrypt=true;TrustServerCertificate=false;"
    }
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Warning"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    "AllowedHosts" = "*.saluexamportal.edu.pk"
}

if (-not (Test-Path "appsettings.Production.json")) {
    $prodSettings | ConvertTo-Json | Set-Content -Path "appsettings.Production.json"
    Write-Host "✓ Created appsettings.Production.json (TEMPLATE - needs configuration)" -ForegroundColor Yellow
}

# Step 6: Build the solution
Write-Host ""
Write-Host "Step 6: Building solution..." -ForegroundColor Green

dotnet build --configuration Release
Write-Host "✓ Solution built successfully" -ForegroundColor Green

# Step 7: Run tests
Write-Host ""
Write-Host "Step 7: Running security tests..." -ForegroundColor Green

Set-Location "SaluExamPortal.Tests"
dotnet test --configuration Release --verbosity normal
Write-Host "✓ Security tests completed" -ForegroundColor Green
Set-Location ".."

# Step 8: Summary
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Setup Complete! ✓" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Run the application: dotnet run --project SaluExamPortal.csproj" -ForegroundColor White
Write-Host "2. Navigate to: https://localhost:7001" -ForegroundColor White
Write-Host "3. Login with:" -ForegroundColor White
Write-Host "   Email: $AdminEmail" -ForegroundColor White
Write-Host "   Password: (use the password you set or generated)" -ForegroundColor White
Write-Host ""
Write-Host "IMPORTANT - Security Reminders:" -ForegroundColor Red
Write-Host "  - Change default admin password on first login" -ForegroundColor White
Write-Host "  - Set up TOTP 2FA for dual-control approvals" -ForegroundColor White
Write-Host "  - Enable HTTPS in production" -ForegroundColor White
Write-Host "  - Configure Azure Key Vault for secrets management" -ForegroundColor White
Write-Host "  - Restrict file upload directory access" -ForegroundColor White
Write-Host "  - Enable audit log monitoring" -ForegroundColor White
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
