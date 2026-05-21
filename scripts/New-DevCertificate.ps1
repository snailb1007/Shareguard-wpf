<#
.SYNOPSIS
    Creates a self-signed code signing certificate for local MSIX development,
    exports it to PFX, and imports it into the Trusted People store.

.DESCRIPTION
    The certificate Subject MUST match the Publisher field in Package.appxmanifest
    exactly: CN=ShareGuard

    After running this script, the local machine will trust side-loaded MSIX
    packages signed with this certificate.

.PARAMETER OutputPath
    Directory to save the exported PFX file. Defaults to ./certs/

.PARAMETER Password
    Password for the PFX file. Defaults to "ShareGuardDev123!"

.EXAMPLE
    .\scripts\New-DevCertificate.ps1
    .\scripts\New-DevCertificate.ps1 -OutputPath "C:\certs" -Password "MyPassword"
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\certs"),
    [string]$Password = "ShareGuardDev123!"
)

$ErrorActionPreference = "Stop"

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$pfxPath = Join-Path $OutputPath "ShareGuard_Dev.pfx"
$subject = "CN=ShareGuard"

Write-Host "Creating self-signed certificate with Subject: $subject" -ForegroundColor Cyan

# Create the certificate
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "ShareGuard Development Certificate" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(5)

Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green

# Export to PFX
$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
Write-Host "PFX exported to: $pfxPath" -ForegroundColor Green

# Import into Trusted People (requires elevation)
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    Import-PfxCertificate -FilePath $pfxPath `
        -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" `
        -Password $securePassword | Out-Null
    Write-Host "Certificate imported into Local Machine -> Trusted People" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "WARNING: Run this script as Administrator to import the certificate" -ForegroundColor Yellow
    Write-Host "         into Trusted People for MSIX side-loading." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Manual import command (run elevated):" -ForegroundColor Yellow
    Write-Host "  Import-PfxCertificate -FilePath `"$pfxPath`" -CertStoreLocation `"Cert:\LocalMachine\TrustedPeople`" -Password (ConvertTo-SecureString -String `"$Password`" -Force -AsPlainText)" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Subject:     $subject"
Write-Host "Thumbprint:  $($cert.Thumbprint)"
Write-Host "PFX Path:    $pfxPath"
Write-Host "PFX Password: $Password"
Write-Host ""
Write-Host "Use this PFX to sign the MSIX package:" -ForegroundColor Cyan
Write-Host "  msbuild ShareGuard.Package\ShareGuard.Package.wapproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=true /p:PackageCertificateKeyFile=`"$pfxPath`" /p:PackageCertificatePassword=`"$Password`""
