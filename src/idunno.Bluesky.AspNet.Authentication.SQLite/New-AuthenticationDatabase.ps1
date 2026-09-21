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

$sqlite = Get-Command -Name 'sqlite3' -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $sqlite)
{
    throw "The sqlite3 command-line tool is required and must be available on PATH. It can be installed with winget install -e --id SQLite.SQLite"
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
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
if (Test-Path -LiteralPath $databasePath)
{
    throw "A database already exists at '$databasePath'."
}

$schema = Get-Content -LiteralPath $schemaPath -Raw
$commands = "BEGIN IMMEDIATE;`n$schema`nCOMMIT;"

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
