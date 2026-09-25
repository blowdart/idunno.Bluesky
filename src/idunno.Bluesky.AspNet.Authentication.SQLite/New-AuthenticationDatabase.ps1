[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputDirectory = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$databaseFileName = 'idunno.Bluesky.AspNet.Authentication.db'
$schemaPath = Join-Path -Path $PSScriptRoot -ChildPath 'schema.sql'

if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf))
{
    throw "The SQLite schema file was not found at '$schemaPath'."
}

# PATH can hold more than one sqlite3, so the first match is taken rather than the whole set. Invoking the set would
# fail reporting every match joined together as a single command name, which says nothing about the real problem.
$sqlite =
    Get-Command -Name 'sqlite3' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1

if ($null -eq $sqlite)
{
    throw "The sqlite3 command-line tool is required and must be available on PATH. It can be installed with winget install -e --id SQLite.SQLite"
}

# PowerShell tracks its own location, which .NET knows nothing about, so resolving a relative path through .NET would
# place the database under whichever directory the process happened to start in rather than the caller's location.
$resolvedOutputDirectory = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $resolvedOutputDirectory)
{
    if (-not (Test-Path -LiteralPath $resolvedOutputDirectory -PathType Container))
    {
        throw "The output path '$resolvedOutputDirectory' is not a directory."
    }
}
else
{
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
}

$databasePath = Join-Path -Path $resolvedOutputDirectory -ChildPath $databaseFileName

# Creating the file here, rather than testing for it and leaving sqlite3 to create it later, closes the window in which
# two concurrent runs could both conclude that no database existed. An empty file is a valid, empty SQLite database.
try
{
    [System.IO.File]::Open(
        $databasePath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None).Dispose()
}
catch [System.IO.IOException]
{
    # CreateNew reports an existing file as an IOException, but so do unrelated failures such as a full disk, which
    # would be thoroughly misleading if reported as the database already existing.
    if (Test-Path -LiteralPath $databasePath)
    {
        throw "A database already exists at '$databasePath'."
    }

    throw
}

$schema = Get-Content -LiteralPath $schemaPath -Raw

# Without bail sqlite3 reports a failing statement and then carries on to the COMMIT, so the transaction alone does not
# make this all or nothing. Bailing leaves the transaction open, and an unclosed transaction is discarded on exit.
$commands = ".bail on`nBEGIN IMMEDIATE;`n$schema`nCOMMIT;"

try
{
    $commands | & $sqlite.Source $databasePath
    if ($LASTEXITCODE -ne 0)
    {
        throw "sqlite3 exited with code $LASTEXITCODE."
    }
}
catch
{
    if (Test-Path -LiteralPath $databasePath)
    {
        Remove-Item -LiteralPath $databasePath -Force
    }

    throw
}

Write-Output $databasePath
