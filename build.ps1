param(
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86")]
    [string]$Platform = "x64",
    [string]$InstallerVersion,
    [string]$VersionSourceProject = (Join-Path $PSScriptRoot "xmlssort\xmlssort.csproj"),
    [string]$ArtifactsRoot = (Join-Path $PSScriptRoot "artifacts"),
    [switch]$SignArtifacts,
    [string]$SignToolPath,
    [string]$CertificateThumbprint,
    [string]$CertificateFilePath,
    [string]$CertificatePassword,
    [string]$TimestampUrl
)

$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "installer\Installer.Build.psm1") -Force

& (Join-Path $PSScriptRoot "installer\Generate-BrandingAssets.ps1")

$platformInfo = Resolve-BuildPlatform -Platform $Platform
$runtimeIdentifier = $platformInfo.RuntimeIdentifier
$signingConfiguration = Resolve-SigningConfiguration -SignArtifactsSpecified $PSBoundParameters.ContainsKey('SignArtifacts') -SignArtifactsEnabled $SignArtifacts.IsPresent -SignToolPath $SignToolPath -CertificateThumbprint $CertificateThumbprint -CertificateFilePath $CertificateFilePath -CertificatePassword $CertificatePassword -TimestampUrl $TimestampUrl

if ([string]::IsNullOrWhiteSpace($InstallerVersion)) {
    $InstallerVersion = Get-InstallerVersion -ProjectPath $VersionSourceProject
}

$publishRoot = Join-Path $ArtifactsRoot (Join-Path "publish" $runtimeIdentifier)
$installerRoot = Join-Path $ArtifactsRoot (Join-Path "installer" $runtimeIdentifier)

$uiPublishDir = Join-Path $publishRoot "xmldiff.UI"
$xmldiffPublishDir = Join-Path $publishRoot "xmldiff"
$xmlssortPublishDir = Join-Path $publishRoot "xmlssort"

Invoke-DotNetPublish -ProjectPath (Join-Path $PSScriptRoot "xmldiff.UI\xmldiff.UI.csproj") -OutputPath $uiPublishDir -AdditionalProperties @(
    "-p:IncludeNativeLibrariesForSelfExtract=true"
) -Configuration $Configuration -RuntimeIdentifier $runtimeIdentifier

Invoke-DotNetPublish -ProjectPath (Join-Path $PSScriptRoot "xmldiff\xmldiff.csproj") -OutputPath $xmldiffPublishDir -Configuration $Configuration -RuntimeIdentifier $runtimeIdentifier
Invoke-DotNetPublish -ProjectPath (Join-Path $PSScriptRoot "xmlssort\xmlssort.csproj") -OutputPath $xmlssortPublishDir -Configuration $Configuration -RuntimeIdentifier $runtimeIdentifier

if ($signingConfiguration.SignArtifacts) {
    Invoke-CodeSigning -FilePaths @(
        (Join-Path $uiPublishDir "xmldiff.UI.exe"),
        (Join-Path $xmldiffPublishDir "xmldiff.exe"),
        (Join-Path $xmlssortPublishDir "xmlssort.exe")
    ) -SignToolPath $signingConfiguration.SignToolPath -CertificateThumbprint $signingConfiguration.CertificateThumbprint -CertificateFilePath $signingConfiguration.CertificateFilePath -CertificatePassword $signingConfiguration.CertificatePassword -TimestampUrl $signingConfiguration.TimestampUrl
}

if (Test-Path $installerRoot) {
    Remove-Item $installerRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

$installerProjectPath = Join-Path $PSScriptRoot "installer\xmlssort.Installer.wixproj"
& dotnet build $installerProjectPath -c $Configuration -o $installerRoot -p:InstallerVersion=$InstallerVersion -p:InstallerPlatform=$($platformInfo.InstallerPlatform) -p:UiPublishDir=$uiPublishDir -p:XmlDiffPublishDir=$xmldiffPublishDir -p:XmlSortPublishDir=$xmlssortPublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed for the installer project."
}

$msiPath = Join-Path $installerRoot ("xmlssort-installer-" + $platformInfo.InstallerPlatform + ".msi")

if ($signingConfiguration.SignArtifacts) {
    Invoke-CodeSigning -FilePaths @($msiPath) -SignToolPath $signingConfiguration.SignToolPath -CertificateThumbprint $signingConfiguration.CertificateThumbprint -CertificateFilePath $signingConfiguration.CertificateFilePath -CertificatePassword $signingConfiguration.CertificatePassword -TimestampUrl $signingConfiguration.TimestampUrl
}

Write-Host "Installer build completed."
Write-Host "Version: $InstallerVersion"
Write-Host "Platform: $Platform"
Write-Host "Signing enabled: $($signingConfiguration.SignArtifacts) [$($signingConfiguration.Sources.SignArtifacts)]"
Write-Host "Publish outputs: $publishRoot"
Write-Host "Installer output: $msiPath"
