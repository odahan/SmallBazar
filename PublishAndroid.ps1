[CmdletBinding()]
param(
    [string]$AndroidSdkDirectory = $env:ANDROID_HOME,
    [string]$KeyStorePath,
    [string]$KeyAlias,
    [string]$KeyPasswordEnvironmentVariable = 'SMALLBAZAR_ANDROID_KEY_PASSWORD',
    [string]$StorePasswordEnvironmentVariable = 'SMALLBAZAR_ANDROID_STORE_PASSWORD'
)

$ErrorActionPreference = 'Stop'
if (-not $AndroidSdkDirectory) { $AndroidSdkDirectory = $env:ANDROID_SDK_ROOT }
if ($AndroidSdkDirectory) { $AndroidSdkDirectory = (Resolve-Path -LiteralPath $AndroidSdkDirectory).Path }
if ($KeyStorePath -and -not $KeyAlias) { throw 'Specify -KeyAlias with -KeyStorePath.' }
if ($KeyAlias -and -not $KeyStorePath) { throw 'Specify -KeyStorePath with -KeyAlias.' }
$arguments = @('publish', 'SmallBazar.csproj', '-m:1', '-f', 'net10.0-android', '-c', 'Release',
    '-p:TargetFrameworks=net10.0-android', '-p:AndroidPackageFormats=apk', '-o', 'bin/publish/android')
if ($AndroidSdkDirectory) { $arguments += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
if ($KeyStorePath) {
    $KeyStorePath = (Resolve-Path -LiteralPath $KeyStorePath).Path
    foreach ($variable in @($KeyPasswordEnvironmentVariable, $StorePasswordEnvironmentVariable)) {
        if ($variable -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or -not [Environment]::GetEnvironmentVariable($variable)) {
            throw 'Set the specified Android signing password environment variables before publishing.'
        }
    }
    $arguments += @('-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$KeyStorePath", "-p:AndroidSigningKeyAlias=$KeyAlias",
        "-p:AndroidSigningKeyPass=env:$KeyPasswordEnvironmentVariable", "-p:AndroidSigningStorePass=env:$StorePasswordEnvironmentVariable")
} else {
    Write-Warning 'Using the Android SDK development signing key. Use the same key as the installed app for local updates; supply a private release keystore for distribution.'
}

Push-Location -LiteralPath $PSScriptRoot
try {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Android publication failed: $LASTEXITCODE." }
    $queryArguments = @('msbuild', 'SmallBazar.csproj', '-m:1', '-p:TargetFramework=net10.0-android',
        '-p:TargetFrameworks=net10.0-android', '-p:Configuration=Release', '-t:ResolveReferences',
        '-getProperty:ApplicationDisplayVersion,ApplicationVersion,ApplicationId,AndroidSdkDirectory')
    if ($AndroidSdkDirectory) { $queryArguments += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
    $properties = & dotnet @queryArguments
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Android package properties.' }
    $values = ($properties | Out-String | ConvertFrom-Json).Properties
    $AndroidSdkDirectory = $values.AndroidSdkDirectory
    if (-not $AndroidSdkDirectory) { throw 'Could not resolve the Android SDK directory.' }
    $apk = Join-Path $PSScriptRoot "bin/publish/android/$($values.ApplicationId)-Signed.apk"
    if (-not (Test-Path -LiteralPath $apk)) { throw 'The signed APK was not produced.' }
    $buildTools = Get-ChildItem -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools') -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $buildTools) { throw 'Android SDK build-tools are required for signature validation.' }
    $signerName = if ($env:OS -eq 'Windows_NT') { 'apksigner.bat' } else { 'apksigner' }
    & (Join-Path $buildTools.FullName $signerName) verify $apk
    if ($LASTEXITCODE -ne 0) { throw 'APK signature validation failed.' }
    $aaptName = if ($env:OS -eq 'Windows_NT') { 'aapt.exe' } else { 'aapt' }
    $metadata = & (Join-Path $buildTools.FullName $aaptName) dump badging $apk
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the APK manifest.' }
    $packageLine = $metadata | Where-Object { $_ -like 'package:*' } | Select-Object -First 1
    if ($packageLine -notmatch "name='$([regex]::Escape($values.ApplicationId))'" -or
        $packageLine -notmatch "versionCode='$([regex]::Escape($values.ApplicationVersion))'" -or
        $packageLine -notmatch "versionName='$([regex]::Escape($values.ApplicationDisplayVersion))'") {
        throw 'Unexpected APK identity or version.'
    }
    $outputDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Publish/Android'))
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $destination = Join-Path $outputDirectory "SmallBazar-$($values.ApplicationDisplayVersion)-code$($values.ApplicationVersion).apk"
    Copy-Item -LiteralPath $apk -Destination $destination -Force
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($destination))" | Set-Content -LiteralPath "$destination.sha256" -Encoding ascii
    Get-Item -LiteralPath $destination
} finally { Pop-Location }
