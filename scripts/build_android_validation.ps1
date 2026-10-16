param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$validationApplicationId = 'com.enaxos.smallbazar.sqlitevalidation'
$projectDirectory = Split-Path -Parent $PSScriptRoot

Push-Location -LiteralPath $projectDirectory
try {
    $buildArguments = @(
        'build',
        'SmallBazar.csproj',
        '-m:1',
        '-t:Rebuild',
        '-f', 'net10.0-android',
        '-c', $Configuration,
        "-p:ApplicationId=$validationApplicationId"
    )
    if ($Configuration -eq 'Debug') {
        $buildArguments += '-p:EmbedAssembliesIntoApk=true'
    }

    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Android validation build failed with exit code $LASTEXITCODE."
    }

    Get-Item -LiteralPath "bin/$Configuration/net10.0-android/$validationApplicationId-Signed.apk"
}
finally {
    Pop-Location
}
