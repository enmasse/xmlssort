Set-StrictMode -Version Latest

function Invoke-ExternalCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($ArgumentList -join ' ')"
    }
}

function Get-EnvironmentVariableValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name)

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value
}

function ConvertTo-OptionalBoolean {
    [CmdletBinding()]
    param(
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        '1' { return $true }
        'true' { return $true }
        'yes' { return $true }
        'on' { return $true }
        '0' { return $false }
        'false' { return $false }
        'no' { return $false }
        'off' { return $false }
        default { throw "Invalid boolean value '$Value' for $Name." }
    }
}

function Resolve-SigningConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [bool]$SignArtifactsSpecified,

        [Parameter(Mandatory = $true)]
        [bool]$SignArtifactsEnabled,

        [string]$SignToolPath,

        [string]$CertificateThumbprint,
        [string]$CertificateFilePath,
        [string]$CertificatePassword,
        [string]$TimestampUrl
    )

    $envSignArtifacts = ConvertTo-OptionalBoolean -Value (Get-EnvironmentVariableValue -Name 'XMLSSORT_SIGN_ARTIFACTS') -Name 'XMLSSORT_SIGN_ARTIFACTS'
    $resolvedThumbprint = if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) { $CertificateThumbprint } else { Get-EnvironmentVariableValue -Name 'XMLSSORT_CERTIFICATE_THUMBPRINT' }
    $resolvedCertificateFilePath = if (-not [string]::IsNullOrWhiteSpace($CertificateFilePath)) { $CertificateFilePath } else { Get-EnvironmentVariableValue -Name 'XMLSSORT_CERTIFICATE_FILE_PATH' }
    $resolvedCertificatePassword = if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) { $CertificatePassword } else { Get-EnvironmentVariableValue -Name 'XMLSSORT_CERTIFICATE_PASSWORD' }
    $resolvedSignToolPath = if (-not [string]::IsNullOrWhiteSpace($SignToolPath)) { $SignToolPath } else { (Get-EnvironmentVariableValue -Name 'XMLSSORT_SIGNTOOL_PATH') ?? 'signtool.exe' }
    $resolvedTimestampUrl = if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) { $TimestampUrl } else { (Get-EnvironmentVariableValue -Name 'XMLSSORT_TIMESTAMP_URL') ?? 'http://timestamp.digicert.com' }

    $signingEnabled = if ($SignArtifactsSpecified) {
        $SignArtifactsEnabled
    }
    elseif ($null -ne $envSignArtifacts) {
        $envSignArtifacts
    }
    else {
        -not [string]::IsNullOrWhiteSpace($resolvedThumbprint) -or -not [string]::IsNullOrWhiteSpace($resolvedCertificateFilePath)
    }

    return [pscustomobject]@{
        SignArtifacts = $signingEnabled
        SignToolPath = $resolvedSignToolPath
        CertificateThumbprint = $resolvedThumbprint
        CertificateFilePath = $resolvedCertificateFilePath
        CertificatePassword = $resolvedCertificatePassword
        TimestampUrl = $resolvedTimestampUrl
        Sources = [pscustomobject]@{
            SignArtifacts = if ($SignArtifactsSpecified) { 'argument' } elseif ($null -ne $envSignArtifacts) { 'environment' } elseif ($signingEnabled) { 'detected' } else { 'default' }
            SignToolPath = if (-not [string]::IsNullOrWhiteSpace($SignToolPath)) { 'argument' } elseif (Get-EnvironmentVariableValue -Name 'XMLSSORT_SIGNTOOL_PATH') { 'environment' } else { 'default' }
            CertificateThumbprint = if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) { 'argument' } elseif (Get-EnvironmentVariableValue -Name 'XMLSSORT_CERTIFICATE_THUMBPRINT') { 'environment' } else { 'unset' }
            CertificateFilePath = if (-not [string]::IsNullOrWhiteSpace($CertificateFilePath)) { 'argument' } elseif (Get-EnvironmentVariableValue -Name 'XMLSSORT_CERTIFICATE_FILE_PATH') { 'environment' } else { 'unset' }
            TimestampUrl = if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) { 'argument' } elseif (Get-EnvironmentVariableValue -Name 'XMLSSORT_TIMESTAMP_URL') { 'environment' } else { 'default' }
        }
    }
}

function Resolve-BuildPlatform {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('x64', 'x86')]
        [string]$Platform
    )

    switch ($Platform) {
        'x64' {
            return [pscustomobject]@{
                Platform = 'x64'
                RuntimeIdentifier = 'win-x64'
                InstallerPlatform = 'x64'
            }
        }
        'x86' {
            return [pscustomobject]@{
                Platform = 'x86'
                RuntimeIdentifier = 'win-x86'
                InstallerPlatform = 'x86'
            }
        }
    }
}

function Get-InstallerVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    if (-not (Test-Path $ProjectPath)) {
        throw "Version source project not found: $ProjectPath"
    }

    [xml]$projectXml = Get-Content -Path $ProjectPath -Raw
    $propertyGroups = @($projectXml.Project.PropertyGroup)
    $rawVersion = $null

    foreach ($propertyGroup in $propertyGroups) {
        $versionNode = $propertyGroup.SelectSingleNode('Version')

        if ($null -ne $versionNode -and -not [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
            $rawVersion = $versionNode.InnerText
            break
        }

        $versionPrefixNode = $propertyGroup.SelectSingleNode('VersionPrefix')

        if ($null -ne $versionPrefixNode -and -not [string]::IsNullOrWhiteSpace($versionPrefixNode.InnerText)) {
            $rawVersion = $versionPrefixNode.InnerText
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "No Version or VersionPrefix metadata was found in '$ProjectPath'."
    }

    $normalizedVersion = ($rawVersion -split '[-+]')[0]
    $version = [Version]$normalizedVersion
    $build = if ($version.Build -lt 0) { 0 } else { $version.Build }
    return "$($version.Major).$($version.Minor).$build"
}

function Invoke-DotNetPublish {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,

        [string[]]$AdditionalProperties = @()
    )

    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

    $arguments = @(
        'publish',
        $ProjectPath,
        '-c', $Configuration,
        '-r', $RuntimeIdentifier,
        '--self-contained', 'true',
        '-o', $OutputPath,
        '-p:PublishSingleFile=true',
        '-p:DebugSymbols=false',
        '-p:DebugType=None'
    ) + $AdditionalProperties

    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList $arguments
}

function Invoke-CodeSigning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$FilePaths,

        [Parameter(Mandatory = $true)]
        [string]$SignToolPath,

        [string]$CertificateThumbprint,
        [string]$CertificateFilePath,
        [string]$CertificatePassword,
        [string]$TimestampUrl
    )

    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint) -and [string]::IsNullOrWhiteSpace($CertificateFilePath)) {
        throw 'Code signing requires either CertificateThumbprint or CertificateFilePath.'
    }

    if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint) -and -not [string]::IsNullOrWhiteSpace($CertificateFilePath)) {
        throw 'Specify either CertificateThumbprint or CertificateFilePath, not both.'
    }

    foreach ($filePath in $FilePaths) {
        if (-not (Test-Path $filePath)) {
            throw "Cannot sign missing file: $filePath"
        }

        $arguments = @('sign', '/fd', 'SHA256')

        if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
            $arguments += @('/tr', $TimestampUrl, '/td', 'SHA256')
        }

        if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
            $arguments += @('/sha1', $CertificateThumbprint)
        }
        else {
            $arguments += @('/f', $CertificateFilePath)

            if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
                $arguments += @('/p', $CertificatePassword)
            }
        }

        $arguments += $filePath
        Invoke-ExternalCommand -FilePath $SignToolPath -ArgumentList $arguments
    }
}

Export-ModuleMember -Function Resolve-BuildPlatform, Get-InstallerVersion, Invoke-DotNetPublish, Invoke-CodeSigning, Resolve-SigningConfiguration
