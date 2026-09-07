#!/bin/bash

# SaluExamPortal Security Fixes - Setup Script
# This script configures all security settings for development and production environments
# Usage: bash setup-security.sh [environment] [admin-email] [admin-password]

set -e

ENVIRONMENT=${1:-Development}
ADMIN_EMAIL=${2:-admin@saluexamportal.edu.pk}
ADMIN_PASSWORD=${3:-$(openssl rand -base64 32)}

echo "=========================================="
echo "SaluExamPortal Security Configuration"
echo "=========================================="
echo "Environment: $ENVIRONMENT"
echo "Admin Email: $ADMIN_EMAIL"
echo "Admin Password: (hidden for security)"
echo ""

# Function to detect OS
detect_os() {
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        echo "linux"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo "macos"
    elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        echo "windows"
    else
        echo "unknown"
    fi
}

OS=$(detect_os)
echo "Detected OS: $OS"
echo ""

# Step 1: Set User Secrets for Development
if [ "$ENVIRONMENT" = "Development" ] || [ "$ENVIRONMENT" = "development" ]; then
    echo "Step 1: Configuring User Secrets (Development)..."
    
    # Initialize user secrets if not already done
    if ! dotnet user-secrets list --project SaluExamPortal.csproj > /dev/null 2>&1; then
        dotnet user-secrets init --project SaluExamPortal.csproj
        echo "✓ User secrets initialized"
    fi
    
    # Set admin credentials
    dotnet user-secrets set "SeedAdmin:Email" "$ADMIN_EMAIL" --project SaluExamPortal.csproj
    dotnet user-secrets set "SeedAdmin:Password" "$ADMIN_PASSWORD" --project SaluExamPortal.csproj
    echo "✓ Admin credentials set in user secrets"
fi

# Step 2: Create database backup (Production only)
if [ "$ENVIRONMENT" = "Production" ] || [ "$ENVIRONMENT" = "production" ]; then
    echo "Step 2: Backing up existing database (Production)..."
    
    BACKUP_DIR="./backups"
    mkdir -p "$BACKUP_DIR"
    
    BACKUP_FILE="$BACKUP_DIR/SaluExamPortal_$(date +%Y%m%d_%H%M%S).bak"
    
    # SQL Server backup command (requires sqlcmd or SQL Server Management Studio)
    # Adjust connection string as needed
    echo "⚠ Manual database backup required for production"
    echo "  Backup location: $BACKUP_FILE"
    echo "  Run: sqlcmd -S (localdb)\\mssqllocaldb -Q \"BACKUP DATABASE aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235 TO DISK='$BACKUP_FILE'\""
    echo ""
fi

# Step 3: Create EF Core migration
echo "Step 3: Creating EF Core migration..."

if ! dotnet ef migrations list --project SaluExamPortal.csproj 2>/dev/null | grep -q "AddTotpAndSecurityFields"; then
    dotnet ef migrations add AddTotpAndSecurityFields --project SaluExamPortal.csproj
    echo "✓ Migration created: AddTotpAndSecurityFields"
else
    echo "✓ Migration already exists: AddTotpAndSecurityFields"
fi

# Step 4: Apply database migration
echo "Step 4: Applying database migration..."

dotnet ef database update --project SaluExamPortal.csproj
echo "✓ Database migration applied successfully"

# Step 5: Restore original web.config if it exists
echo "Step 5: Verifying configuration files..."

if [ "$ENVIRONMENT" = "Production" ]; then
    if [ ! -f "Properties/launchSettings.json" ]; then
        echo "⚠ launchSettings.json not found - Create this file with production settings"
    else
        echo "✓ launchSettings.json found"
    fi
fi

# Step 6: Create sample environment configuration
echo "Step 6: Creating sample configuration files..."

# Create sample development appsettings
if [ ! -f "appsettings.Development.json" ]; then
cat > appsettings.Development.json << 'EOF'
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Authentication": "Debug"
    }
  },
  "AllowedHosts": "*"
}
EOF
    echo "✓ Created appsettings.Development.json"
fi

# Create sample production appsettings (template - must be filled in)
if [ ! -f "appsettings.Production.json" ]; then
cat > appsettings.Production.json << 'EOF'
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER_NAME;Database=SaluExamPortal;User Id=sa;Password=YOUR_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*.saluexamportal.edu.pk"
}
EOF
    echo "✓ Created appsettings.Production.json (TEMPLATE - needs configuration)"
    echo "⚠ IMPORTANT: Update connection string and admin credentials in Production environment"
fi

# Step 7: Build the solution
echo "Step 7: Building solution..."

dotnet build --configuration Release
echo "✓ Solution built successfully"

# Step 8: Run tests
echo "Step 8: Running security tests..."

cd SaluExamPortal.Tests
dotnet test --configuration Release
echo "✓ Security tests completed"
cd ..

# Step 9: Summary
echo ""
echo "=========================================="
echo "Setup Complete! ✓"
echo "=========================================="
echo ""
echo "Next Steps:"
echo "1. Run the application: dotnet run --project SaluExamPortal.csproj"
echo "2. Navigate to: https://localhost:7001"
echo "3. Login with:"
echo "   Email: $ADMIN_EMAIL"
echo "   Password: (use the password you set or generated)"
echo ""
echo "IMPORTANT - Security Reminders:"
echo "  - Change default admin password on first login"
echo "  - Set up TOTP 2FA for dual-control approvals"
echo "  - Enable HTTPS in production"
echo "  - Configure Azure Key Vault for secrets management"
echo "  - Restrict file upload directory access"
echo "  - Enable audit log monitoring"
echo ""
echo "=========================================="
