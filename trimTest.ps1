#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Checks supported idunno packages for trimming and Native AOT problems.

.DESCRIPTION
    Runs three complementary checks:

    * Whole-assembly trimming and Native AOT analysis for every package which claims compatibility.
    * Offline execution of trimmed and Native AOT binaries which call public, unauthenticated AT Protocol and Bluesky APIs.
    * The same offline smoke test against locally packed NuGet packages.

    The Razor Pages UI package is explicitly excluded because ASP.NET Core Razor Pages do not support trimming or Native AOT.

.PARAMETER Rid
    The runtime identifier to publish for. Defaults to the runtime identifier of the machine running the script.

.PARAMETER RequireNativeBinary
    Treat a missing or unusable platform linker as a failure. CI should pass this because its native toolchain is provisioned.

.PARAMETER Diagnostic
    Raise MSBuild output to diagnostic verbosity. Binary logs are always written beneath trimming/artifacts/logs.

.EXAMPLE
    ./trimTest.ps1

.EXAMPLE
    ./trimTest.ps1 -Rid linux-x64 -RequireNativeBinary
#>

[CmdletBinding()]
param (
    [string] $Rid,
    [switch] $RequireNativeBinary,
    [switch] $Diagnostic
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$trimmingPath = Join-Path -Path $PSScriptRoot -ChildPath 'trimming'
$artifactsPath = Join-Path -Path $trimmingPath -ChildPath 'artifacts'
$logsPath = Join-Path -Path $artifactsPath -ChildPath 'logs'
$publishPath = Join-Path -Path $artifactsPath -ChildPath 'publish'
$packageFeedPath = Join-Path -Path $artifactsPath -ChildPath 'packages'
$packageCachePath = Join-Path -Path $artifactsPath -ChildPath 'package-cache'
$packageNuGetConfigPath = Join-Path -Path $artifactsPath -ChildPath 'package-consumer.nuget.config'
$sizeReportPath = Join-Path -Path $artifactsPath -ChildPath 'size-report.md'

$coreAnalysisProject = Join-Path -Path $trimmingPath -ChildPath 'CoreAnalysis' |
    Join-Path -ChildPath 'idunno.Trimming.CoreAnalysis.csproj'
$authenticationAnalysisProject = Join-Path -Path $trimmingPath -ChildPath 'AuthenticationAnalysis' |
    Join-Path -ChildPath 'idunno.Trimming.AuthenticationAnalysis.csproj'
$publicApiSmokeProject = Join-Path -Path $trimmingPath -ChildPath 'PublicApiSmoke' |
    Join-Path -ChildPath 'idunno.Trimming.PublicApiSmoke.csproj'
$packageConsumerSmokeProject = Join-Path -Path $trimmingPath -ChildPath 'PackageConsumerSmoke' |
    Join-Path -ChildPath 'idunno.Trimming.PackageConsumerSmoke.csproj'
$packageAnalysisProject = Join-Path -Path $trimmingPath -ChildPath 'PackageAnalysis' |
    Join-Path -ChildPath 'idunno.Trimming.PackageAnalysis.csproj'

$analysisProjects = @(
    @{
        Name = 'core'
        Path = $coreAnalysisProject
    },
    @{
        Name = 'authentication'
        Path = $authenticationAnalysisProject
    }
)

$supportedProjects = @(
    'src/idunno.AtProto.Types/idunno.AtProto.Types.csproj',
    'src/idunno.AtProto/idunno.AtProto.csproj',
    'src/idunno.AtProto.OAuthCallback/idunno.AtProto.OAuthCallback.csproj',
    'src/idunno.Bluesky/idunno.Bluesky.csproj',
    'src/idunno.Bluesky.AspNet.Authentication/idunno.Bluesky.AspNet.Authentication.csproj',
    'src/idunno.Bluesky.AspNet.Authentication.MySQL/idunno.Bluesky.AspNet.Authentication.MySQL.csproj',
    'src/idunno.Bluesky.AspNet.Authentication.Redis/idunno.Bluesky.AspNet.Authentication.Redis.csproj',
    'src/idunno.Bluesky.AspNet.Authentication.SQLite/idunno.Bluesky.AspNet.Authentication.SQLite.csproj'
)

$excludedProjects = @{
    'src/idunno.Bluesky.AspNet.Authentication.UI/idunno.Bluesky.AspNet.Authentication.UI.csproj' =
        'Razor Pages do not support trimming or Native AOT.'
}

if ([string]::IsNullOrWhiteSpace($Rid))
{
    $Rid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
}

if ($IsWindows)
{
    $vsInstallerDirectory = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath 'Microsoft Visual Studio' |
        Join-Path -ChildPath 'Installer'

    if ((Test-Path -Path (Join-Path -Path $vsInstallerDirectory -ChildPath 'vswhere.exe')) -and
        (($env:PATH -split [System.IO.Path]::PathSeparator) -notcontains $vsInstallerDirectory))
    {
        $env:PATH = $vsInstallerDirectory + [System.IO.Path]::PathSeparator + $env:PATH
    }
}

if (Test-Path -Path $artifactsPath)
{
    Remove-Item -Path $artifactsPath -Recurse -Force
}

New-Item -Path $logsPath -ItemType Directory -Force | Out-Null
New-Item -Path $publishPath -ItemType Directory -Force | Out-Null
New-Item -Path $packageFeedPath -ItemType Directory -Force | Out-Null
New-Item -Path $packageCachePath -ItemType Directory -Force | Out-Null

$escapedPackageFeedPath = [System.Security.SecurityElement]::Escape($packageFeedPath)
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="trimming-packages" value="$escapedPackageFeedPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="trimming-packages">
      <package pattern="idunno.AtProto" />
      <package pattern="idunno.AtProto.Types" />
      <package pattern="idunno.AtProto.OAuthCallback" />
      <package pattern="idunno.Bluesky" />
      <package pattern="idunno.Bluesky.AspNet.Authentication" />
      <package pattern="idunno.Bluesky.AspNet.Authentication.MySQL" />
      <package pattern="idunno.Bluesky.AspNet.Authentication.Redis" />
      <package pattern="idunno.Bluesky.AspNet.Authentication.SQLite" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -Path $packageNuGetConfigPath

function ConvertTo-RepositoryPath
{
    param (
        [Parameter(Mandatory)]
        [string] $Path
    )

    return [System.IO.Path]::GetRelativePath($PSScriptRoot, $Path).Replace('\', '/')
}

function Assert-SetEqual
{
    param (
        [Parameter(Mandatory)]
        [string] $Description,
        [Parameter(Mandatory)]
        [string[]] $Expected,
        [Parameter(Mandatory)]
        [string[]] $Actual
    )

    $difference = @(Compare-Object -ReferenceObject $Expected -DifferenceObject $Actual)

    if ($difference.Count -ne 0)
    {
        Write-Host "$Description is out of date." -ForegroundColor Red
        $difference | Format-Table -AutoSize | Out-Host
        exit 1
    }
}

function Invoke-DotNet
{
    param (
        [Parameter(Mandatory)]
        [string] $Description,
        [Parameter(Mandatory)]
        [string[]] $Arguments,
        [switch] $AllowMissingNativeLinker
    )

    Write-Host ''
    Write-Host $Description -ForegroundColor Cyan

    $previousErrorActionPreference = $ErrorActionPreference

    if (Test-Path -Path 'variable:PSNativeCommandUseErrorActionPreference')
    {
        $previousNativeCommandUseErrorActionPreference = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }

    try
    {
        $ErrorActionPreference = 'Continue'

        & dotnet @Arguments 2>&1 |
            Tee-Object -Variable commandOutput |
            ForEach-Object { Write-Host $_ }
        $commandExitCode = $LASTEXITCODE
    }
    finally
    {
        $ErrorActionPreference = $previousErrorActionPreference

        if (Test-Path -Path 'variable:previousNativeCommandUseErrorActionPreference')
        {
            $PSNativeCommandUseErrorActionPreference = $previousNativeCommandUseErrorActionPreference
        }
    }

    $outputLines = @($commandOutput | ForEach-Object { $_.ToString() })
    $diagnostics = @($outputLines | Where-Object { $_ -match '\bIL\d{4}\b' })

    if ($diagnostics.Count -ne 0)
    {
        Write-Host ''
        Write-Host "Trimming and Native AOT analysis reported $($diagnostics.Count) diagnostics." -ForegroundColor Red
        $diagnostics | ForEach-Object { Write-Host $_ }
        exit 1
    }

    if ($commandExitCode -eq 0)
    {
        return $true
    }

    $commandText = $outputLines -join [Environment]::NewLine

    if ($AllowMissingNativeLinker -and
        (($commandText -match 'Platform linker.*not found') -or ($commandText -match 'MSB3073')))
    {
        Write-Host 'Native analysis passed, but a platform linker was not available.' -ForegroundColor Yellow
        return $false
    }

    Write-Host "$Description failed with exit code $commandExitCode." -ForegroundColor Red
    exit $commandExitCode
}

function Invoke-Publish
{
    param (
        [Parameter(Mandatory)]
        [string] $Name,
        [Parameter(Mandatory)]
        [string] $Project,
        [Parameter(Mandatory)]
        [string] $Framework,
        [Parameter(Mandatory)]
        [bool] $Aot,
        [string[]] $AdditionalArguments = @()
    )

    $mode = if ($Aot) { 'aot' } else { 'trimmed' }
    $outputPath = Join-Path -Path $publishPath -ChildPath "$Name-$Framework-$mode"
    $binaryLogPath = Join-Path -Path $logsPath -ChildPath "$Name-$Framework-$mode.binlog"

    $arguments = @(
        'publish',
        $Project,
        '--configuration', 'Release',
        '--framework', $Framework,
        '--runtime', $Rid,
        '--self-contained', 'true',
        '--output', $outputPath,
        "-bl:$binaryLogPath",
        '-p:PublishTrimmed=true',
        "-p:PublishAot=$($Aot.ToString().ToLowerInvariant())",
        '-p:TrimmerSingleWarn=false'
    )

    if ($Diagnostic)
    {
        $arguments += '--verbosity'
        $arguments += 'diagnostic'
    }

    $arguments += $AdditionalArguments

    $allowMissingNativeLinker = $Aot -and -not $RequireNativeBinary
    $published = Invoke-DotNet `
        -Description "Publishing $Name for $Framework in $mode mode." `
        -Arguments $arguments `
        -AllowMissingNativeLinker:$allowMissingNativeLinker

    return @{
        Name = "$Name $Framework $mode"
        OutputPath = $outputPath
        Published = $published
    }
}

function Invoke-SmokeExecutable
{
    param (
        [Parameter(Mandatory)]
        [hashtable] $PublishResult,
        [Parameter(Mandatory)]
        [string] $ExecutableName
    )

    if (-not $PublishResult.Published)
    {
        return
    }

    $executableFileName = if ($IsWindows) { "$ExecutableName.exe" } else { $ExecutableName }
    $executablePath = Join-Path -Path $PublishResult.OutputPath -ChildPath $executableFileName

    if (-not (Test-Path -Path $executablePath -PathType Leaf))
    {
        Write-Host "Expected smoke executable $executablePath was not produced." -ForegroundColor Red
        exit 1
    }

    Write-Host ''
    Write-Host "Running $($PublishResult.Name)." -ForegroundColor Cyan
    & $executablePath

    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }
}

function Get-PackageVersion
{
    $packages = @(
        Get-ChildItem -Path $packageFeedPath -Filter 'idunno.Bluesky.*.nupkg' |
            Where-Object { $_.Name -match '^idunno\.Bluesky\.\d' }
    )

    if ($packages.Count -ne 1)
    {
        Write-Host "Expected one idunno.Bluesky package, but found $($packages.Count)." -ForegroundColor Red
        exit 1
    }

    $prefix = 'idunno.Bluesky.'
    return $packages[0].Name.Substring(
        $prefix.Length,
        $packages[0].Name.Length - $prefix.Length - '.nupkg'.Length)
}

$actualProjects = @(
    Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'src') -Filter '*.csproj' -Recurse |
        ForEach-Object { ConvertTo-RepositoryPath -Path $_.FullName } |
        Sort-Object
)
$classifiedProjects = @($supportedProjects + $excludedProjects.Keys | Sort-Object)
Assert-SetEqual -Description 'The trimming package classification' -Expected $actualProjects -Actual $classifiedProjects

$expectedRootAssemblies = @(
    $supportedProjects |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) } |
        Sort-Object
)
$actualRootAssemblies = @(
    $analysisProjects |
        ForEach-Object {
            [xml] $project = Get-Content -Path $_.Path
            $project.Project.ItemGroup.TrimmerRootAssembly |
                ForEach-Object { $_.Include }
        } |
        Sort-Object
)
Assert-SetEqual -Description 'The whole-assembly analysis roots' -Expected $expectedRootAssemblies -Actual $actualRootAssemblies

foreach ($excludedProject in $excludedProjects.GetEnumerator())
{
    $projectPath = Join-Path -Path $PSScriptRoot -ChildPath $excludedProject.Key

    foreach ($property in @('IsTrimmable', 'IsAotCompatible'))
    {
        $propertyValue = (& dotnet msbuild $projectPath "-getProperty:$property" '-p:TargetFramework=net10.0' -nologo).Trim()

        if ($LASTEXITCODE -ne 0 -or $propertyValue -ne 'false')
        {
            Write-Host "$($excludedProject.Key) must set $property to false. $($excludedProject.Value)" -ForegroundColor Red
            exit 1
        }
    }
}

$publishResults = [System.Collections.Generic.List[hashtable]]::new()

foreach ($framework in @('net9.0', 'net10.0'))
{
    foreach ($analysisProject in $analysisProjects)
    {
        $publishResults.Add((Invoke-Publish `
            -Name "$($analysisProject.Name)-analysis" `
            -Project $analysisProject.Path `
            -Framework $framework `
            -Aot $false))
    }

    $smokeResult = Invoke-Publish `
        -Name 'public-api-smoke' `
        -Project $publicApiSmokeProject `
        -Framework $framework `
        -Aot $false
    $publishResults.Add($smokeResult)
    Invoke-SmokeExecutable -PublishResult $smokeResult -ExecutableName 'idunno.Trimming.PublicApiSmoke'
}

foreach ($analysisProject in $analysisProjects)
{
    $publishResults.Add((Invoke-Publish `
        -Name "$($analysisProject.Name)-analysis" `
        -Project $analysisProject.Path `
        -Framework 'net10.0' `
        -Aot $true))
}

$nativeSmokeResult = Invoke-Publish `
    -Name 'public-api-smoke' `
    -Project $publicApiSmokeProject `
    -Framework 'net10.0' `
    -Aot $true
$publishResults.Add($nativeSmokeResult)
Invoke-SmokeExecutable -PublishResult $nativeSmokeResult -ExecutableName 'idunno.Trimming.PublicApiSmoke'

foreach ($packageProject in $supportedProjects)
{
    $packLogName = [System.IO.Path]::GetFileNameWithoutExtension($packageProject)
    $packArguments = @(
        'pack',
        (Join-Path -Path $PSScriptRoot -ChildPath $packageProject),
        '--configuration', 'Release',
        '--output', $packageFeedPath,
        "-bl:$(Join-Path -Path $logsPath -ChildPath "$packLogName-pack.binlog")"
    )

    [void](Invoke-DotNet -Description "Packing $packLogName." -Arguments $packArguments)
}

$packageVersion = Get-PackageVersion
$previousNuGetPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = $packageCachePath

try
{
    $packageArguments = @(
        '-p:UsePackedPackages=true',
        "-p:IdunnoPackageVersion=$packageVersion",
        '--configfile', $packageNuGetConfigPath
    )

    foreach ($framework in @('net9.0', 'net10.0'))
    {
        $packageAnalysisResult = Invoke-Publish `
            -Name 'package-analysis' `
            -Project $packageAnalysisProject `
            -Framework $framework `
            -Aot $false `
            -AdditionalArguments $packageArguments
        $publishResults.Add($packageAnalysisResult)

        $packageSmokeResult = Invoke-Publish `
            -Name 'package-consumer-smoke' `
            -Project $packageConsumerSmokeProject `
            -Framework $framework `
            -Aot $false `
            -AdditionalArguments $packageArguments
        $publishResults.Add($packageSmokeResult)
        Invoke-SmokeExecutable -PublishResult $packageSmokeResult -ExecutableName 'idunno.Trimming.PackageConsumerSmoke'
    }

    $nativePackageAnalysisResult = Invoke-Publish `
        -Name 'package-analysis' `
        -Project $packageAnalysisProject `
        -Framework 'net10.0' `
        -Aot $true `
        -AdditionalArguments $packageArguments
    $publishResults.Add($nativePackageAnalysisResult)

    $nativePackageSmokeResult = Invoke-Publish `
        -Name 'package-consumer-smoke' `
        -Project $packageConsumerSmokeProject `
        -Framework 'net10.0' `
        -Aot $true `
        -AdditionalArguments $packageArguments
    $publishResults.Add($nativePackageSmokeResult)
    Invoke-SmokeExecutable -PublishResult $nativePackageSmokeResult -ExecutableName 'idunno.Trimming.PackageConsumerSmoke'
}
finally
{
    $env:NUGET_PACKAGES = $previousNuGetPackages
}

$sizeReport = @(
    '## Trimming and Native AOT output sizes',
    '',
    '| Output | Files | Size |',
    '|---|---:|---:|'
)

foreach ($publishResult in $publishResults | Where-Object { $_.Published })
{
    $files = @(Get-ChildItem -Path $publishResult.OutputPath -File -Recurse)
    $totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
    $size = if ($totalBytes -ge 1MB)
    {
        '{0:N2} MB' -f ($totalBytes / 1MB)
    }
    else
    {
        '{0:N2} KB' -f ($totalBytes / 1KB)
    }

    $sizeReport += "| $($publishResult.Name) | $($files.Count) | $size |"
}

$sizeReport | Set-Content -Path $sizeReportPath

Write-Host ''
Write-Host 'All trimming and Native AOT checks passed.' -ForegroundColor Green
Write-Host "Output sizes were written to $sizeReportPath."
