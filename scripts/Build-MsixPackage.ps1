<#
.SYNOPSIS
    Builds and signs the ShareGuard MSIX package.

.DESCRIPTION
    Restores, builds, and packages the ShareGuard application into a signed
    .msix file using MSBuild and the Windows Application Packaging Project.

.PARAMETER Configuration
    Build configuration. Default: Release

.PARAMETER Platform
    Target platform. Default: x64

.PARAMETER CertificatePath
    Path to the PFX certificate file for signing.

.PARAMETER CertificatePassword
    Password for the PFX certificate.

.PARAMETER OutputDirectory
    Directory for the generated MSIX package. Default: ./artifacts/

.PARAMETER SkipSigning
    If set, builds the package without signing (for CI testing).

.EXAMPLE
    .\scripts\Build-MsixPackage.ps1 -CertificatePath .\certs\ShareGuard_Dev.pfx -CertificatePassword "ShareGuardDev123!"
    .\scripts\Build-MsixPackage.ps1 -SkipSigning
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts"),
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path $PSScriptRoot "..\ShareGuard.Package\ShareGuard.Package.wapproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Packaging project not found at: $projectPath"
    exit 1
}

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " ShareGuard MSIX Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Platform:      $Platform"
Write-Host "Signing:       $(if ($SkipSigning) { 'Disabled' } else { 'Enabled' })"
Write-Host "Output:        $OutputDirectory"
Write-Host ""

# Find MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    $msbuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
}

if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
    Write-Error "MSBuild not found. Install Visual Studio with the '.NET desktop development' workload."
    exit 1
}

Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Gray

# Build MSBuild arguments
$msbuildArgs = @(
    $projectPath,
    "/t:Rebuild",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:UapAppxPackageBuildMode=SideloadOnly",
    "/p:AppxPackageDir=$OutputDirectory\",
    "/p:GenerateAppInstallerFile=false",
    "/verbosity:minimal",
    "/restore"
)

if (-not $SkipSigning) {
    if (-not $CertificatePath -or -not (Test-Path $CertificatePath)) {
        Write-Error "Certificate path is required for signed builds. Use -SkipSigning to build without signing, or provide -CertificatePath."
        exit 1
    }
    $msbuildArgs += "/p:AppxPackageSigningEnabled=true"
    $msbuildArgs += "/p:PackageCertificateKeyFile=$CertificatePath"
    if ($CertificatePassword) {
        $msbuildArgs += "/p:PackageCertificatePassword=$CertificatePassword"
    }
} else {
    $msbuildArgs += "/p:AppxPackageSigningEnabled=false"
}

Write-Host ""
Write-Host "Building MSIX package..." -ForegroundColor Cyan

& $msbuildPath @msbuildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "MSBuild failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Find the output package
$msixFiles = Get-ChildItem -Path $OutputDirectory -Filter "*.msix" -Recurse
if ($msixFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Build Successful!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    foreach ($file in $msixFiles) {
        $sizeMB = [math]::Round($file.Length / 1MB, 2)
        Write-Host "  $($file.FullName) ($sizeMB MB)"
    }
} else {
    Write-Host ""
    Write-Host "Build completed but no .msix file found in $OutputDirectory" -ForegroundColor Yellow
    Write-Host "Check the build output above for details." -ForegroundColor Yellow
}
