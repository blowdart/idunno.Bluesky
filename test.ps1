<#
.SYNOPSIS
Runs the clean Debug build and full test suite against disposable databases.

.DESCRIPTION
Requires PowerShell 7 and Docker with Linux containers. Creates unique, loopback-only PostgreSQL 17 and
MySQL 8.4 servers with temporary storage, verifies authenticated access from the test runner, runs every
test project and framework with the default test parallelism, and removes only this run's containers in finally.
Logs, the build binary log, JUnit results, and elapsed times are retained under the ignored .artifacts/test-runs directory.
The build uses default MSBuild parallelism and shared compilation. By default it is incremental and skips analyzers;
pass -Full for the clean, analyzer-enabled Debug build that matches CI. PostgreSQL is limited to two CPUs and MySQL to six,
4 GiB of memory without additional swap, and a 2 GiB temporary data filesystem.

.PARAMETER Full
Runs the clean (--no-incremental) build with analyzers enabled, and every target framework, instead of the fast local run.

.PARAMETER AllFrameworks
Runs the tests on every target framework. By default the fast local run tests net10.0 only; -Full implies this.
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [switch] $Full,
    [switch] $AllFrameworks
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$suffix = [Guid]::NewGuid().ToString('N')
$logDirectory = Join-Path $PSScriptRoot ".artifacts/test-runs/$suffix"
$containers = [System.Collections.Generic.List[string]]::new()
$environmentNames = @(
    'POSTGRES_PASSWORD',
    'MYSQL_ROOT_PASSWORD',
    'LABELER_TEST_POSTGRESQL_CONNECTION',
    'LABELER_TEST_MYSQL_CONNECTION',
    'BOTSDK_TEST_POSTGRESQL_CONNECTION',
    'BOTSDK_TEST_MYSQL_CONNECTION'
)
$previousEnvironment = @{}
$total = [Diagnostics.Stopwatch]::StartNew()

function Invoke-DotNet
{
    param([string] $Name, [string[]] $Arguments)

    $logPath = Join-Path $logDirectory "$Name.log"
    Write-Host "Running $Name. Log: $logPath"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    & dotnet @Arguments *> $logPath
    $result = $LASTEXITCODE
    $watch.Stop()
    Get-Content -LiteralPath $logPath -Tail 12 | Out-Host
    "$Name seconds: $($watch.Elapsed.TotalSeconds); exit code: $result" |
        Tee-Object -FilePath (Join-Path $logDirectory 'timing.log') -Append | Out-Host
    if ($result -ne 0)
    {
        throw "$Name failed with exit code $result. Logs: $logDirectory"
    }
}

Push-Location $PSScriptRoot
try
{
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
    foreach ($name in $environmentNames)
    {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    & docker info --format '{{.ServerVersion}}' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Docker with Linux containers must be running.' }
    # The default is an incremental build without analyzers for fast local iteration. -Full is the clean,
    # analyzer-enabled build that matches CI.
    $buildArguments = @(
        'build', 'idunno.Bluesky.slnx', '-c', 'Debug', '--verbosity', 'quiet',
        '-p:UseSharedCompilation=true', '-maxcpucount', "-bl:$logDirectory\build-{}.binlog")
    if ($Full)
    {
        $buildArguments += '--no-incremental'
    }
    else
    {
        $buildArguments += '-p:RunAnalyzers=false'
    }
    Invoke-DotNet -Name 'build' -Arguments $buildArguments

    $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $env:POSTGRES_PASSWORD = $password
    $env:MYSQL_ROOT_PASSWORD = $password
    $postgres = "idunno-tests-$suffix-pg"
    $mysql = "idunno-tests-$suffix-mysql"
    & docker run --detach --name $postgres --env POSTGRES_PASSWORD --publish '127.0.0.1::5432' `
        --cpus=2 --memory=4g --memory-swap=4g `
        --tmpfs '/var/lib/postgresql/data:rw,size=2147483648' postgres:17 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Disposable PostgreSQL startup failed.' }
    $containers.Add($postgres)
    & docker run --detach --name $mysql --env MYSQL_ROOT_PASSWORD --env 'MYSQL_ROOT_HOST=%' `
        --publish '127.0.0.1::3306' --cpus=6 --memory=4g --memory-swap=4g `
        --tmpfs '/var/lib/mysql:rw,size=2147483648' mysql:8.4 `
        --max-connections=200 --innodb-redo-log-capacity=536870912 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Disposable MySQL startup failed.' }
    $containers.Add($mysql)

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(3)
    do
    {
        & docker exec $postgres sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -U postgres -d postgres -tAc "SELECT 1"' *> $null
        $pgReady = $LASTEXITCODE -eq 0
        & docker exec $mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql --protocol=TCP -h 127.0.0.1 -uroot -N -e "SELECT 1"' *> $null
        $myReady = $LASTEXITCODE -eq 0
        if ($pgReady -and $myReady) { break }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    if (!$pgReady -or !$myReady) { throw 'Disposable databases did not become ready for authenticated TCP access.' }

    $pgBinding = & docker port $postgres 5432/tcp
    if ($LASTEXITCODE -ne 0 -or $pgBinding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'PostgreSQL loopback binding was not found.' }
    $pgPort = $Matches[1]
    $myBinding = & docker port $mysql 3306/tcp
    if ($LASTEXITCODE -ne 0 -or $myBinding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'MySQL loopback binding was not found.' }
    $myPort = $Matches[1]
    $env:LABELER_TEST_POSTGRESQL_CONNECTION = "Host=127.0.0.1;Port=$pgPort;Username=postgres;Password=$password;Database=postgres;SSL Mode=Disable"
    $env:LABELER_TEST_MYSQL_CONNECTION = "Server=127.0.0.1;Port=$myPort;User ID=root;Password=$password;SslMode=None;AllowPublicKeyRetrieval=True"
    $env:BOTSDK_TEST_POSTGRESQL_CONNECTION = $env:LABELER_TEST_POSTGRESQL_CONNECTION
    $env:BOTSDK_TEST_MYSQL_CONNECTION = $env:LABELER_TEST_MYSQL_CONNECTION
    Invoke-DotNet -Name 'database-preflight' -Arguments @(
        'test', '--project', 'test/idunno.Bluesky.Labeler.Test/idunno.Bluesky.Labeler.Test.csproj',
        '--no-build', '--no-restore', '--max-parallel-test-modules', '1', '--max-threads', '1',
        '--filter-method', '*.RefusesRotationWithoutARegisteredHost',
        "-bl:$logDirectory\database-preflight-{}.binlog")
    $testArguments = @(
        'test', '--no-build', '--no-restore', '--results-directory', "$logDirectory\results", '--report-xunit-junit',
        "-bl:$logDirectory\tests-{}.binlog")
    if (-not ($Full -or $AllFrameworks))
    {
        $testArguments += @('--framework', 'net10.0')
    }
    Invoke-DotNet -Name 'tests' -Arguments $testArguments
}
finally
{
    $cleanupFailed = $false
    foreach ($container in $containers)
    {
        & docker inspect --format '{{.Name}} memory={{.HostConfig.Memory}} memorySwap={{.HostConfig.MemorySwap}} nanoCpus={{.HostConfig.NanoCpus}} tmpfs={{json .HostConfig.Tmpfs}} oomKilled={{.State.OOMKilled}} exitCode={{.State.ExitCode}}' `
            $container *> (Join-Path $logDirectory "$container-resources.log")
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not capture resource diagnostics for $container." }
        & docker logs $container *> (Join-Path $logDirectory "$container.log")
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not capture logs for $container." }
        & docker rm --force --volumes $container | Out-Null
        if ($LASTEXITCODE -ne 0)
        {
            $cleanupFailed = $true
            Write-Warning "Cleanup failed for $container. Remove this run-owned container explicitly."
        }
    }
    foreach ($name in $previousEnvironment.Keys)
    {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    $total.Stop()
    "Total build/setup/tests/cleanup: $($total.Elapsed)" |
        Tee-Object -FilePath (Join-Path $logDirectory 'timing.log') -Append | Out-Host
    Pop-Location
    if ($cleanupFailed) { throw "Disposable database cleanup failed. Logs: $logDirectory" }
}
