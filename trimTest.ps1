#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Checks idunno.AtProto and idunno.Bluesky for trimming and native AOT problems.

.DESCRIPTION
    Publishes the trimming test project with trimming and native AOT enabled. The publish itself is the test:
    ILLink and ILC walk everything the test project touches and report an IL diagnostic for any call which is
    not safe to trim or to compile ahead of time. TreatWarningsAsErrors turns those diagnostics into errors.

    Producing the native binary needs a platform linker, which the .NET SDK does not carry. Analysis does not.
    ILLink and ILC ship as NuGet packages, run before the linker is invoked, and report every diagnostic
    whether or not a linker is available, so a machine with only the .NET SDK still gets complete trimming and
    AOT coverage. It just cannot produce a runnable binary.

    The two failures are therefore reported differently. A diagnostic is a problem with the libraries and fails
    this script. A linker which is absent or cannot be found leaves the analysis results intact, and is reported
    as a warning rather than a failure unless -RequireNativeBinary is passed.

.PARAMETER Rid
    The runtime identifier to publish for. Defaults to the runtime identifier of the machine running the script.

.PARAMETER RequireNativeBinary
    Treat the native binary as part of the test, so that a missing or unusable platform linker fails the script
    rather than warning. Continuous integration builds, where the toolchain is expected to be present and a
    missing one indicates a broken agent, should pass this.

.PARAMETER Diagnostic
    Raise MSBuild output to diagnostic verbosity. The binary log is written whether or not this is specified,
    and is the better starting point, so this is rarely needed.

.EXAMPLE
    ./trimTest.ps1

    Publishes for the current machine's runtime identifier, reporting a missing linker as a warning.

.EXAMPLE
    ./trimTest.ps1 -Rid linux-x64 -RequireNativeBinary

    Publishes for linux-x64 and requires the native binary to be produced.
#>

[CmdletBinding()]
param (
    [string] $Rid,
    [switch] $RequireNativeBinary,
    [switch] $Diagnostic
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path -Path $PSScriptRoot -ChildPath 'trimming' | Join-Path -ChildPath 'idunno.TrimmingTest.csproj'
$binaryLogPath = Join-Path -Path $PSScriptRoot -ChildPath 'trimming' | Join-Path -ChildPath 'trimmingTest.binlog'

if ([string]::IsNullOrWhiteSpace($Rid))
{
    $Rid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
}

# The ILCompiler targets find the MSVC linker by running findvcvarsall.bat, which calls Visual Studio's
# vcvarsall.bat and redirects only its standard output. vcvarsall.bat runs vswhere.exe without qualifying it,
# and vswhere.exe installs under the 32 bit program files directory on the system drive even when Visual Studio
# itself does not, so on a machine where Visual Studio is installed elsewhere that call fails and cmd reports it
# on standard error, which the redirect does not cover. MSBuild captures both streams as the console output, so
# the error text ends up prefixed to the linker directory the targets parse out of it, and the build then tries
# to run a linker whose path is an error message. Putting vswhere.exe on the path lets that internal lookup
# succeed, so nothing is written to standard error and the output stays parseable.
if ($IsWindows)
{
    $vsInstallerDirectory = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath 'Microsoft Visual Studio' | Join-Path -ChildPath 'Installer'

    if ((Test-Path -Path (Join-Path -Path $vsInstallerDirectory -ChildPath 'vswhere.exe')) -and
        (($env:PATH -split [System.IO.Path]::PathSeparator) -notcontains $vsInstallerDirectory))
    {
        $env:PATH = $vsInstallerDirectory + [System.IO.Path]::PathSeparator + $env:PATH
    }
}

$arguments = @(
    'publish'
    $projectPath
    '--configuration', 'Release'
    '--runtime', $Rid
    "-bl:$binaryLogPath"
)

if ($Diagnostic)
{
    $arguments += '-v:diag'
}

Write-Host "Publishing $projectPath for $Rid."

# dotnet is expected to fail here, and the exit code and the output are the results this script interprets, so
# neither a non zero exit code nor anything written to standard error may raise a terminating error. Native
# command exit codes honour $ErrorActionPreference from PowerShell 7.4 onwards.
$previousErrorActionPreference = $ErrorActionPreference

if (Test-Path -Path 'variable:PSNativeCommandUseErrorActionPreference')
{
    $previousNativeCommandUseErrorActionPreference = $PSNativeCommandUseErrorActionPreference
    $PSNativeCommandUseErrorActionPreference = $false
}

try
{
    $ErrorActionPreference = 'Continue'

    & dotnet @arguments 2>&1 | Tee-Object -Variable publishOutput
    $publishExitCode = $LASTEXITCODE
}
finally
{
    $ErrorActionPreference = $previousErrorActionPreference

    if (Test-Path -Path 'variable:previousNativeCommandUseErrorActionPreference')
    {
        $PSNativeCommandUseErrorActionPreference = $previousNativeCommandUseErrorActionPreference
    }
}

$publishText = ($publishOutput | Out-String)
$diagnostics = @($publishOutput | ForEach-Object { $_.ToString() } | Where-Object { $_ -match 'IL\d{4}' })

if ($diagnostics.Count -gt 0)
{
    Write-Host ''
    Write-Host "Trimming and AOT analysis reported $($diagnostics.Count) diagnostics." -ForegroundColor Red
    Write-Host "A binary log has been written to $binaryLogPath."
    Write-Host ''
    Write-Host 'If a diagnostic is a false positive, suppress it with UnconditionalSuppressMessage, not with'
    Write-Host 'SuppressMessage, which is conditional on CODE_ANALYSIS and so is never written into the assembly'
    Write-Host 'for the trimmer to read. Apply it to the member whose IL the diagnostic names. A static field'
    Write-Host 'initializer is compiled into the generated static constructor, so a suppression on the field'
    Write-Host 'itself is never consulted and belongs on the declaring type.'

    exit 1
}

if ($publishExitCode -eq 0)
{
    Write-Host ''
    Write-Host 'Trimming and AOT analysis reported no diagnostics, and the native binary was produced.' -ForegroundColor Green

    exit 0
}

# A platform linker which is missing or unusable leaves the analysis results intact, so it is reported
# separately from a diagnostic. The ILCompiler targets report it in one of two ways. Both Windows and Unix raise
# a "Platform linker not found" error when no linker can be located at all. When a linker path is produced but
# cannot be run, the Exec task which invokes it reports MSB3073 instead, which is what the redirection problem
# described above produces.
if ((($publishText -match 'Platform linker.*not found') -or ($publishText -match 'MSB3073')) -and -not $RequireNativeBinary)
{
    Write-Host ''
    Write-Host 'Trimming and AOT analysis reported no diagnostics, so the libraries are trimming and AOT clean.' -ForegroundColor Green
    Write-Host 'The native binary could not be linked because a platform linker was not available.' -ForegroundColor Yellow

    if ($IsWindows)
    {
        Write-Host 'Install the Desktop development with C++ workload, either through Visual Studio or through the'
        Write-Host 'standalone Visual Studio Build Tools, to produce a runnable binary.'
    }
    elseif ($IsMacOS)
    {
        Write-Host 'Install the Xcode command line tools, with xcode-select --install, to produce a runnable binary.'
    }
    else
    {
        Write-Host 'Install clang and zlib1g-dev to produce a runnable binary.'
    }

    exit 0
}

Write-Host ''
Write-Host "Publish failed with exit code $publishExitCode." -ForegroundColor Red
Write-Host "A binary log has been written to $binaryLogPath."

exit $publishExitCode
