# SiteKit CLI Build Script
param(
    [string]$Configuration = "Release",
    [switch]$Pack,
    [switch]$Install,
    [switch]$Clean
)

Write-Host "SiteKit CLI Build Script" -ForegroundColor Green

if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean
    Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Building SiteKit CLI..." -ForegroundColor Yellow
dotnet build -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green

if ($Pack) {
    Write-Host "Packaging SiteKit CLI..." -ForegroundColor Yellow
    dotnet pack -c $Configuration --no-build
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Pack failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Package created successfully!" -ForegroundColor Green
}

if ($Install) {
    Write-Host "Installing SiteKit CLI globally..." -ForegroundColor Yellow
    
    # Uninstall existing version if it exists
    dotnet tool uninstall -g SiteKit.CLI 2>$null
    
    # Install from local package
    $packagePath = Get-ChildItem -Path "bin/$Configuration" -Filter "*.nupkg" | Select-Object -First 1
    if ($packagePath) {
        dotnet tool install -g SiteKit.CLI --add-source "bin/$Configuration"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SiteKit CLI installed successfully!" -ForegroundColor Green
            Write-Host "You can now use: sitekit deploy -s MySite" -ForegroundColor Cyan
        } else {
            Write-Host "Installation failed!" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Package not found. Run with -Pack first." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Done!" -ForegroundColor Green 