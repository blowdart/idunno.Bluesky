#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Recaptures the benchmark corpora, verifies them, and suggests new allocation budgets.

.DESCRIPTION
    1. Captures fresh corpora from the live, public, unauthenticated firehose, jetstream and AppView, and the home
       timeline of the account given by -Handle and -Password. Only message payloads and response bodies are recorded,
       and anything which looks like a credential is discarded. Timeline entries which mention the account's handle or
       password are removed, and the account's DID is replaced with a placeholder.
    2. Runs the corpus tests, which check message counts and rescan every message for credentials. A failure stops the
       script, and the new corpora must not be committed. When credentials are available every corpus is also checked
       to make sure it does not contain the handle or password.
    3. Runs the allocation budget tests on every target framework against the new corpora and prints what each path now
       allocates per message, call or item, alongside a suggested budget roughly ten percent higher.

    Commit the new corpora together with any budget changes in the AllocationBudgetTests.cs files in
    test/idunno.AtProto.Test and test/idunno.Bluesky.Test.

.PARAMETER Handle
    The handle of the account whose timeline is captured. Defaults to the _BlueskyHandle environment variable.

.PARAMETER Password
    The password, or preferably an app password, of the account whose timeline is captured. Defaults to the
    _BlueskyPassword environment variable. Credentials are passed to the capture through environment variables, never
    on a command line, and are never written to the corpora.

.PARAMETER XrpcOnly
    Only recapture the AppView responses, leaving the firehose and jetstream corpora untouched.

.PARAMETER SkipCapture
    Do not capture, just verify the existing corpora and report their allocations.

.EXAMPLE
    ./benchmarks/capture.ps1

.EXAMPLE
    ./benchmarks/capture.ps1 -XrpcOnly -Handle example.bsky.social -Password xxxx-xxxx-xxxx-xxxx
#>

[CmdletBinding()]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'Password', Justification = 'App passwords are pasted as plain text, and the value is only passed on through an environment variable.')]
param (
    [string] $Handle,
    [Alias('AppPassword', 'ApiKey')]
    [string] $Password,
    [switch] $XrpcOnly,
    [switch] $SkipCapture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $PSScriptRoot 'idunno.AtProto.Benchmarks'
$corpusDirectory = Join-Path $benchmarkProject 'Corpus'
$testProject = Join-Path $repositoryRoot 'test/idunno.AtProto.Test/idunno.AtProto.Test.csproj'
$budgetTestClasses = [ordered]@{
    $testProject = 'idunno.AtProto.Test.AllocationBudgetTests'
    (Join-Path $repositoryRoot 'test/idunno.Bluesky.Test/idunno.Bluesky.Test.csproj') = 'idunno.Bluesky.Test.AllocationBudgetTests'
}

if (-not $Handle) {
    $Handle = $env:_BlueskyHandle
}

if (-not $Password) {
    $Password = $env:_BlueskyPassword
}

if (-not $SkipCapture) {
    $missing = @()
    if ([string]::IsNullOrWhiteSpace($Handle)) {
        $missing += 'a handle (-Handle, or the _BlueskyHandle environment variable)'
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        $missing += 'a password or app password (-Password, or the _BlueskyPassword environment variable)'
    }

    if ($missing) {
        $verb = if ($missing.Count -gt 1) { 'were' } else { 'was' }
        Write-Error -ErrorAction Continue "Capturing the getTimeline corpus needs a Bluesky account, but $($missing -join ' and ') $verb not supplied. Use an app password rather than your main password, or run with -SkipCapture to only verify and measure the existing corpora."
        exit 1
    }
}

# The capture and the corpus tests read the credentials from the environment, so they never appear on a command line.
$originalHandle = $env:_BlueskyHandle
$originalPassword = $env:_BlueskyPassword
$env:_BlueskyHandle = $Handle
$env:_BlueskyPassword = $Password
try {
    if (-not $SkipCapture) {
        Write-Host 'Capturing corpora...' -ForegroundColor Cyan
        $captureArguments = @('run', '--configuration', 'Release', '--project', $benchmarkProject, '--', 'capture')
        if ($XrpcOnly) {
            $captureArguments += 'xrpc'
        }

        & dotnet @captureArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Capture failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host 'Verifying corpora...' -ForegroundColor Cyan
    & dotnet test --project $testProject --filter-class idunno.AtProto.Test.BenchmarkCorpusTests
    if ($LASTEXITCODE -ne 0) {
        throw 'The corpus tests failed. Do not commit these corpora.'
    }
}
finally {
    $env:_BlueskyHandle = $originalHandle
    $env:_BlueskyPassword = $originalPassword
}

Write-Host 'Measuring allocations...' -ForegroundColor Cyan
$reportDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("idunno-allocations-" + [guid]::NewGuid().ToString('n'))
$env:IDUNNO_ALLOCATION_REPORT = $reportDirectory
try {
    # A failure here is expected when a new corpus moves allocations past a budget, so report rather than stop.
    $budgetsPassed = $true
    foreach ($budgetTests in $budgetTestClasses.GetEnumerator()) {
        & dotnet test --project $budgetTests.Key --filter-class $budgetTests.Value | Out-Null
        $budgetsPassed = $budgetsPassed -and ($LASTEXITCODE -eq 0)
    }
}
finally {
    Remove-Item Env:\IDUNNO_ALLOCATION_REPORT
}

if (-not (Test-Path $reportDirectory)) {
    throw 'The allocation budget tests did not report any measurements.'
}

$measurements = Get-ChildItem $reportDirectory -Filter '*.txt' | ForEach-Object {
    $framework, $path = $_.BaseName -split '\.', 2
    [pscustomobject]@{ Path = $path; Framework = $framework; Bytes = [long](Get-Content $_.FullName -Raw) }
}
Remove-Item $reportDirectory -Recurse -Force

$frameworks = $measurements.Framework | Sort-Object -Unique | Sort-Object { [int]($_ -replace '^net', '') }
$measurements | Group-Object Path | Sort-Object Name | ForEach-Object {
    $row = [ordered]@{ Path = $_.Name }
    foreach ($framework in $frameworks) {
        $row[$framework] = ($_.Group | Where-Object Framework -eq $framework).Bytes
    }

    # Ten percent above the worst framework, rounded up to the next hundred bytes.
    $worst = ($_.Group | Measure-Object Bytes -Maximum).Maximum
    $row['SuggestedBudget'] = [long]([math]::Ceiling($worst * 1.1 / 100) * 100)
    [pscustomobject]$row
} | Format-Table -AutoSize

if ($budgetsPassed) {
    Write-Host 'All paths are within their current budgets.' -ForegroundColor Green
}
else {
    Write-Host 'Some paths exceed their current budgets. Update the AllocationBudgetTests.cs files with the suggested budgets.' -ForegroundColor Yellow
}

Get-ChildItem $corpusDirectory -Filter '*.bin.gz' | Format-Table Name, @{ Name = 'Bytes'; Expression = { '{0:N0}' -f $_.Length } } -AutoSize
Write-Host 'Commit the corpora and any budget changes together.'