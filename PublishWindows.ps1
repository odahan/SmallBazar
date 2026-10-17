[CmdletBinding()]
param(
    [string]$CertificateThumbprint = $env:SMALLBAZAR_WINDOWS_CERTIFICATE_THUMBPRINT
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { throw 'Windows publication requires Windows.' }
if (-not $CertificateThumbprint) {
    $candidates = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Subject -eq 'CN=enaxos' -and $_.HasPrivateKey -and $_.NotBefore -le (Get-Date) -and $_.NotAfter -gt (Get-Date) -and
        '1.3.6.1.5.5.7.3.3' -in $_.EnhancedKeyUsageList.ObjectId
    })
    if ($candidates.Count -ne 1) { throw 'Specify -CertificateThumbprint for a valid CN=enaxos code-signing certificate in CurrentUser/My.' }
    $CertificateThumbprint = $candidates[0].Thumbprint
}
if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if ($certificate.Subject -ne 'CN=enaxos' -or -not $certificate.HasPrivateKey -or
    $certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date) -or
    '1.3.6.1.5.5.7.3.3' -notin $certificate.EnhancedKeyUsageList.ObjectId) {
    throw 'The certificate must be valid for code signing, include its private key and match CN=enaxos.'
}

Push-Location -LiteralPath $PSScriptRoot
try {
    & dotnet publish SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Release `
        '-p:TargetFrameworks=net10.0-windows10.0.19041.0' '-p:PublishProfile=MSIX-win10-x64' `
        "-p:PackageCertificateThumbprint=$CertificateThumbprint"
    if ($LASTEXITCODE -ne 0) { throw "Windows publication failed: $LASTEXITCODE." }
    $properties = & dotnet msbuild SmallBazar.csproj '-p:TargetFramework=net10.0-windows10.0.19041.0' '-getProperty:ApplicationDisplayVersion,ApplicationVersion,AppxPackageDir' '-p:PublishProfile=MSIX-win10-x64'
    if ($LASTEXITCODE -ne 0) { throw 'Could not read package properties.' }
    $values = ($properties | Out-String | ConvertFrom-Json).Properties
    $version = "$($values.ApplicationDisplayVersion).$($values.ApplicationVersion)"
    $package = Join-Path $values.AppxPackageDir "SmallBazar_${version}_x64_Test/SmallBazar_${version}_x64.msix"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package)
    try {
        $reader = [System.IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
        try { $manifest = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($manifest.Package.Identity.Version -ne $version -or $manifest.Package.Identity.Name -ne 'com.enaxos.smallbazar' -or
            $manifest.Package.Identity.Publisher -ne 'CN=enaxos') { throw 'Unexpected MSIX identity or version.' }
    } finally { $archive.Dispose() }
    $signature = Get-AuthenticodeSignature -LiteralPath $package
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) {
        throw 'MSIX signature validation failed. Ensure Windows trusts the signing certificate.'
    }
    $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($package))" | Set-Content -LiteralPath "$package.sha256" -Encoding ascii
    Get-Item -LiteralPath $package
} finally { Pop-Location }
