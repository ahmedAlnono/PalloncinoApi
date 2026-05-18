@echo off
title Palloncino API Setup - .NET 10.0 Required
color 0A

echo ========================================
echo    Palloncino API - Auto Setup Script
echo ========================================
echo.
echo This script will:
echo   1. Check for .NET 10.0 SDK
echo   2. Clone/Pull the project from GitHub
echo   3. Install all NuGet packages
echo   4. Build the project
echo   5. Create database and run the API
echo.
echo ========================================
echo.

:: ============================================
:: STEP 1: Check .NET 10.0 Installation
:: ============================================
echo [1/6] Checking .NET 10.0 installation...
echo.

:: Get .NET version
for /f "delims=" %%i in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%i

if "%DOTNET_VERSION%"=="" (
    echo [ERROR] .NET SDK is NOT installed!
    echo.
    echo Please install .NET 10.0 SDK from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    echo.
    echo For Windows, download and run:
    echo dotnet-sdk-10.0.1xx-win-x64.exe
    echo.
    echo After installation, restart this script.
    echo.
    pause
    exit /b 1
)

:: Check if version starts with 10.0
echo %DOTNET_VERSION% | findstr /b "10.0" >nul
if %errorlevel% neq 0 (
    echo [ERROR] Wrong .NET version detected!
    echo.
    echo Current version: %DOTNET_VERSION%
    echo Required version: 10.0.x
    echo.
    echo Please install .NET 10.0 SDK from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    echo.
    echo To check your current versions:
    echo   dotnet --list-sdks
    echo   dotnet --list-runtimes
    echo.
    pause
    exit /b 1
)

echo [OK] .NET 10.0 is installed (Version: %DOTNET_VERSION%)
echo.

:: Check if ASP.NET Core Runtime is available
echo Checking ASP.NET Core Runtime...
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 10.0" >nul
if %errorlevel% neq 0 (
    echo [WARNING] ASP.NET Core 10.0 Runtime not found!
    echo Some features may not work correctly.
    echo.
) else (
    echo [OK] ASP.NET Core Runtime available
)
echo.

:: ============================================
:: STEP 2: Clone or Pull Repository
:: ============================================
echo [2/6] Setting up project...
echo.

set REPO_URL=https://github.com/yourusername/palloncino-backend.git
set PROJECT_DIR=palloncino-backend

:: Check if Git is installed
git --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Git is not installed!
    echo.
    echo Please install Git from:
    echo https://git-scm.com/download/win
    echo.
    pause
    exit /b 1
)

if exist "%PROJECT_DIR%" (
    echo Project directory exists. Updating...
    cd "%PROJECT_DIR%"
    git pull
    if %errorlevel% neq 0 (
        echo [WARNING] Git pull failed, using existing code
    )
    cd ..
) else (
    echo Cloning repository...
    git clone %REPO_URL%
    if %errorlevel% neq 0 (
        echo [ERROR] Failed to clone repository
        echo.
        echo Please check the repository URL and your internet connection.
        echo.
        pause
        exit /b 1
    )
)
echo [OK] Project ready
echo.

:: ============================================
:: STEP 3: Navigate to Project
:: ============================================
echo [3/6] Entering project directory...
cd "%PROJECT_DIR%"
echo [OK] Current directory: %CD%
echo.

:: ============================================
:: STEP 4: Restore NuGet Packages
:: ============================================
echo [4/6] Installing NuGet packages...
echo This may take 2-5 minutes depending on your internet speed...
echo.

dotnet restore --verbosity quiet
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Failed to restore packages
    echo.
    echo Try running manually:
    echo   dotnet restore
    echo.
    pause
    exit /b 1
)
echo [OK] Packages installed successfully
echo.

:: ============================================
:: STEP 5: Build the Project
:: ============================================
echo [5/6] Building the project...
echo.

dotnet build --configuration Release --no-restore --verbosity quiet
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed
    echo.
    echo Try running manually:
    echo   dotnet build
    echo.
    pause
    exit /b 1
)
echo [OK] Build successful
echo.

:: ============================================
:: STEP 6: Create Database and Run
:: ============================================
echo [6/6] Setting up database and starting API...
echo.

:: Check if appsettings.json exists
if not exist "appsettings.json" (
    echo [WARNING] appsettings.json not found!
    echo Creating default configuration...
    
    :: Create default appsettings.json if missing
    (
    echo  {
    echo  "Logging": {
    echo    "LogLevel": {
    echo      "Default": "Information",
    echo      "Microsoft.AspNetCore": "Warning",
    echo      "Microsoft.EntityFrameworkCore": "Warning"
    echo    }
    echo  },
    echo  "Serilog": {
    echo    "MinimumLevel": {
    echo      "Default": "Information",
    echo      "Override": {
    echo        "Microsoft": "Warning",
    echo        "System": "Warning"
    echo      }
    echo    }
    echo  },
    echo  "ConnectionStrings": {
    echo    "DefaultConnection": "Data Source=PalloncinoDB.sqlite"
    echo  },
    echo  "Jwt": {
    echo    "Key": "FS4FD4#dfg43DFhs34Fz-0)(-()34GRH%^(+8erGJD",
    echo    "Issuer": "PalloncinoAPI",
    echo    "Audience": "PalloncinoApp",
    echo    "ExpiryInMinutes": 60
    echo  },
    echo  "FileStorage": {
    echo    "UploadPath": "Uploads",
    echo    "MaxSizeInMB": 10
    echo  },
    echo  "Firebase": {
    echo    "ProjectId": "your-firebase-project-id",
    echo    "CredentialPath": "firebase-admin-sdk.json"
    echo  },
    echo  "Stripe": {
    echo    "PublishableKey": "",
    echo    "SecretKey": "",
    echo    "WebhookSecret": "",
    echo    "Currency": "usd",
    echo    "SuccessUrl": "https://palloncino.com/orders/payment/success?session_id={CHECKOUT_SESSION_ID}",
    echo    "CancelUrl": "https://palloncino.com/orders/payment/cancel"
    echo  },
    echo  "BusinessRules": {
    echo    "DefaultDueHour": 18,
    echo    "ReminderHours": 2,
    echo    "MaxChecklistImages": 5,
    echo    "AllowTaskCompletionByOthers": true
    echo  },
    ) > appsettings.json
    echo [OK] Default configuration created
    echo you want to add stripe PublishableKey and SecretKey
)

:: Check if database exists and run migrations
if not exist "PalloncinoDB.sqlite" (
    echo Creating database and applying migrations...
    echo.
    dotnet ef database update --no-build --verbosity quiet
    if %errorlevel% neq 0 (
        echo [WARNING] Migration failed
        echo.
        echo You can run migrations manually later with:
        echo   dotnet ef database update
        echo.
    ) else (
        echo [OK] Database created successfully
    )
) else (
    echo Database exists. Checking for pending migrations...
    dotnet ef database update --no-build --verbosity quiet 2>nul
)
echo.

:: ============================================
:: START THE API
:: ============================================
echo.
echo ========================================
echo    API IS NOW RUNNING!
echo ========================================
echo.
echo Open in your browser:
echo.
echo   Scalar UI:  https://localhost:5001/scalar/v1
echo   OpenAPI JSON: https://localhost:5001/openapi/v1.json
echo.
echo ========================================
echo   IMPORTANT NOTES:
echo ========================================
echo.
echo 1. If you see a certificate warning:
echo    - Run: dotnet dev-certs https --trust
echo    - Or just proceed (development only)
echo.
echo 2. Default admin credentials:
echo    Email: admin@palloncino.com
echo    Password: Admin@123
echo.
echo 3. To stop the API: Press Ctrl+C
echo.
echo ========================================
echo.

:: Try HTTPS first, fallback to HTTP if fails
dotnet run --urls="https://localhost:5001;http://localhost:5000" --no-build --verbosity quiet

:: If HTTPS fails, try HTTP only
if %errorlevel% neq 0 (
    echo.
    echo [WARNING] HTTPS failed, trying HTTP only...
    echo.
    dotnet run --urls="http://localhost:5000" --no-build --verbosity quiet
)

pause