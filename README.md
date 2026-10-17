# SmallBazar

SmallBazar is a personal storage inventory for Windows and Android, built with
.NET 10 and .NET MAUI. It helps you keep track of numbered boxes, their contents
and storage areas.

Features include description and area search, sorting, local SQLite storage,
database backup and restore, and an AI category report. The report uses an
eligible ChatGPT account through Sign in with ChatGPT; no OpenAI API key is
required. Model availability and usage depend on the connected account.

The application follows MVVM with CommunityToolkit.Mvvm. Classification is saved
locally; viewing, searching and exporting a saved report do not generate another
classification. An analysis sends box numbers and descriptions to the service.

## License

SmallBazar is source-available ("sources ouvertes") under the custom
[SmallBazar Personal Use License](LICENSE). An individual may use, build, study,
and privately modify it solely for strictly personal, noncommercial use.

Every other use requires the author's prior written permission, including
professional, commercial, organizational, educational, or nonprofit use, as
well as redistribution of original or modified versions, even free of charge.
Contact the author through [e-naxos](https://www.e-naxos.com/) for permission.
Third-party components retain their own licenses.

## Requirements

- The .NET SDK and MAUI workload set pinned in `global.json` (10.0.401).
- A supported Windows development environment with the Windows SDK for MSIX builds.
- The MAUI Android workload, Android SDK/build-tools and a compatible JDK for APK builds.
- PowerShell for the publication scripts; PowerShell 7 is recommended.
- Private signing material for packages that will be distributed or used to update
  an existing installation. Signing keys are not included in this repository.

Install the MAUI workloads using your development environment, or run
`dotnet workload restore SmallBazar.csproj` after installing the pinned SDK.

## Publish Windows

Run from the repository root:

```powershell
./PublishWindows.ps1 -CertificateThumbprint YOUR_CODE_SIGNING_CERTIFICATE_THUMBPRINT
```

The certificate must be installed in `CurrentUser/My`, include its private key,
allow code signing and match the manifest publisher `CN=enaxos`. You can also set
`SMALLBAZAR_WINDOWS_CERTIFICATE_THUMBPRINT`. If neither is supplied, the script
uses the certificate only when exactly one valid matching certificate exists.

The script produces a self-contained x64 MSIX, checks its identity, version and
signature, and writes a SHA-256 checksum. Results are placed next to the checkout:

```text
SmallBazarSolution/
  SmallBazar/                   # this repository
    PublishWindows.ps1
    PublishAndroid.ps1
  Publish/
    Windows/
      SmallBazar_<version>_x64_Test/
        SmallBazar_<version>_x64.msix
        SmallBazar_<version>_x64.msix.sha256
    Android/
      SmallBazar-<version>-code<code>.apk
      SmallBazar-<version>-code<code>.apk.sha256
```

For Visual Studio, use `Properties/PublishProfiles/MSIX-win10-x64.pubxml` and
select the signing certificate locally. It uses the same Windows output folder.
Keep certificate selections and other machine-specific settings out of Git.
The older `scripts/publish_windows.ps1` entry point delegates to the root script.

Install the MSIX manually on a Windows machine that trusts its signer. The script
does not install packages, change certificate trust or create automatic updates.

## Publish Android

For a local APK using the Android SDK development signing key:

```powershell
./PublishAndroid.ps1
```

For a private release keystore:

```powershell
# Populate these environment variables securely in the current session:
# SMALLBAZAR_ANDROID_KEY_PASSWORD
# SMALLBAZAR_ANDROID_STORE_PASSWORD
./PublishAndroid.ps1 -KeyStorePath 'C:/Private/SmallBazar.keystore' -KeyAlias SmallBazar
```

Passwords are passed to the Android build tasks through `env:` references, not
literal command-line arguments. Alternative environment variable names can be
provided with `-KeyPasswordEnvironmentVariable` and
`-StorePasswordEnvironmentVariable`.

The script uses the SDK selected by .NET (or `-AndroidSdkDirectory` to override
it), publishes a Release APK, verifies its identity, version and signature using Android SDK
build-tools, and copies the signed APK and checksum to `../Publish/Android`.
The default development key is intended for local testing, not public release.
Always preserve the signing key: an existing app can only be updated with a
compatible package signed by the same key and an appropriate version code.

Transfer the APK to your phone for manual installation, or use:

```powershell
adb -s YOUR_DEVICE_SERIAL install -r ../Publish/Android/SmallBazar-<version>-code<code>.apk
```

Publication does not install the APK or modify phone data. Back up the database
before updating an installation in use. Do not run Windows and Android publication
simultaneously in the same checkout: restore uses shared intermediate assets.

## Development and tests

```powershell
dotnet build SmallBazar.csproj -m:1 -f net10.0-android -c Debug -p:TargetFrameworks=net10.0-android
dotnet build SmallBazar.csproj -m:1 -f net10.0-windows10.0.19041.0 -c Debug -p:TargetFrameworks=net10.0-windows10.0.19041.0 -p:AppxPackageSigningEnabled=false
dotnet test tests/SmallBazar.Data.Tests/SmallBazar.Data.Tests.csproj -m:1
dotnet test tests/SmallBazar.ChatGpt.Tests/SmallBazar.ChatGpt.Tests.csproj -m:1
```

Keep `-m:1` on build and test commands. Generated source files belong under `obj`;
there is no `DummyGenerated` directory. Package versions are defined in
`SmallBazar.csproj`; Tools displays the installed version through `AppInfo`.

## Data, credentials and repository hygiene

Database backups contain personal inventory data. Keep them, exported reports,
diagnostic logs, private keystores, PFX files and authentication tokens outside
the source repository. ChatGPT session credentials use the platform's protected
storage and are excluded from Android cloud backup. Do not share diagnostic
evidence without reviewing it first.

`.gitignore` excludes local build/package outputs, `.local` diagnostics, databases
and signing material. It does not erase files already committed to Git history.
Rotate any credential that has previously been committed before publishing the
repository, and review history separately from the current source tree.
