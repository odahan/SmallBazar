param(
    [string]$CertificateThumbprint = $env:SMALLBAZAR_WINDOWS_CERTIFICATE_THUMBPRINT
)

$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path -Parent $PSScriptRoot) 'PublishWindows.ps1') -CertificateThumbprint $CertificateThumbprint
