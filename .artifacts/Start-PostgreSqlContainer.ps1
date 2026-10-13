<#
.SYNOPSIS
Starts a local PostgreSQL container for idunno.Bluesky.AspNet.Authentication.PostgreSQL and prints a .NET connection string.

.DESCRIPTION
Checks that Docker is installed and running, starts a PostgreSQL container with a database and user, applies the
schema.sql shipped with the idunno.Bluesky.AspNet.Authentication.PostgreSQL project, and writes an Npgsql connection
string to the pipeline.

The schema is read from the project rather than repeated here, so that the container cannot drift from the schema the
package actually ships.

This is a development helper. The container publishes only to the loopback address and its data is lost when the
container is removed. Neither is suitable for anything but a local machine.

.PARAMETER ContainerName
The name to give the container. Defaults to idunno-bluesky-postgresql.

.PARAMETER Port
The host port to publish PostgreSQL on. Defaults to 5432.

.PARAMETER Database
The database to create and apply the schema to. Defaults to idunno_bluesky.

.PARAMETER UserName
The user to create. Defaults to idunno.

.PARAMETER Password
The password to set on the created user. A random one is generated when this is not supplied, so that no credential
needs to live in source control.

.PARAMETER Image
The PostgreSQL image to run. Defaults to postgres:17.

.PARAMETER SchemaPath
The schema to apply. Defaults to the schema.sql in the idunno.Bluesky.AspNet.Authentication.PostgreSQL project.

.PARAMETER Force
Removes an existing container of the same name rather than failing. Everything in it is lost.

.EXAMPLE
.\Start-PostgreSqlContainer.ps1

.EXAMPLE
$connectionString = .\Start-PostgreSqlContainer.ps1 -Port 5433 -Force
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ContainerName = 'idunno-bluesky-postgresql',

    [Parameter()]
    [ValidateRange(1, 65535)]
    [int] $Port = 5432,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Database = 'idunno_bluesky',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $UserName = 'idunno',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Password,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Image = 'postgres:17',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $SchemaPath = (Join-Path $PSScriptRoot '..\src\idunno.Bluesky.AspNet.Authentication.PostgreSQL\schema.sql'),

    [Parameter()]
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-PostgreSqlPassword
{
    # A connection string is semicolon delimited with equals separated options, so a password containing either would
    # need escaping by every caller. The alphabet below avoids the problem rather than pushing it downstream.
    $alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $bytes = [byte[]]::new(32)

    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    return -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

# Docker is resolved once, rather than on each call, so that every invocation runs the same executable. On Windows
# PATH holds both docker.exe and an extensionless docker, so the first match is taken rather than the whole set.
$script:DockerPath =
    Get-Command -Name 'docker' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty Source

function Invoke-Docker
{
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $FailureMessage
    )

    # Output is captured rather than discarded, so that a failure can be reported with what Docker actually said.
    $output = & $script:DockerPath @Arguments 2>&1

    if ($LASTEXITCODE -ne 0)
    {
        throw "$FailureMessage docker $($Arguments -join ' ') exited with code $LASTEXITCODE. $output"
    }

    return $output
}

if ($null -eq $script:DockerPath)
{
    throw 'Docker is required and must be available on PATH. It can be installed with winget install -e --id Docker.DockerDesktop'
}

if (-not (Test-Path -Path $SchemaPath -PathType Leaf))
{
    throw "The schema could not be found at '$SchemaPath'. Pass -SchemaPath if the script is being run from outside the repository."
}

$SchemaPath = (Resolve-Path -Path $SchemaPath).Path

Invoke-Docker -Arguments @('info', '--format', '{{.ServerVersion}}') -FailureMessage 'Docker is installed but is not responding. Start Docker Desktop, or the Docker daemon, and try again.' | Out-Null

if (-not $PSBoundParameters.ContainsKey('Password'))
{
    $Password = New-PostgreSqlPassword
}

$existing = Invoke-Docker `
    -Arguments @('ps', '--all', '--quiet', '--filter', "name=^/$ContainerName$") `
    -FailureMessage 'The existing containers could not be listed.'

if (-not [string]::IsNullOrWhiteSpace(($existing -join '')))
{
    if (-not $Force)
    {
        throw "A container named '$ContainerName' already exists. Re-run with -Force to replace it, or remove it with docker rm --force $ContainerName."
    }

    Write-Verbose "Removing the existing '$ContainerName' container."

    Invoke-Docker `
        -Arguments @('rm', '--force', $ContainerName) `
        -FailureMessage "The existing '$ContainerName' container could not be removed." | Out-Null
}

Write-Verbose "Starting '$ContainerName' from $Image on 127.0.0.1:$Port."

# Published to the loopback address rather than every interface, so a development container with a known password is not
# offered to the rest of the network. The password is passed as an environment variable rather than command line
# arguments so that it does not appear in the process list inside the container.
try
{
    Invoke-Docker `
        -Arguments @(
            'run',
            '--detach',
            '--name', $ContainerName,
            '--publish', "127.0.0.1:${Port}:5432",
            '--env', "POSTGRES_DB=$Database",
            '--env', "POSTGRES_USER=$UserName",
            '--env', "POSTGRES_PASSWORD=$Password",
            $Image) `
        -FailureMessage "The '$ContainerName' container could not be started." | Out-Null
}
catch
{
    # A run that fails after the container is created, a clashing published port for example, leaves the container
    # behind in the created state, which would make the next run report that the name is already taken. Removing it
    # here keeps the failure to the real cause. The removal is best effort as the container may never have been created.
    & $script:DockerPath 'rm' '--force' $ContainerName 2>&1 | Out-Null

    throw
}

Write-Verbose "Waiting for '$Database' to accept connections as '$UserName'."

$ready = $false
foreach ($attempt in 1..120)
{
    $probe = & $script:DockerPath `
        'exec' `
        '--env' "PGPASSWORD=$Password" `
        $ContainerName `
        'psql' '--host=127.0.0.1' '--port=5432' '--username' $UserName '--dbname' $Database '--quiet' '--tuples-only' '--command' 'SELECT 1' 2>&1

    if ($LASTEXITCODE -eq 0)
    {
        $ready = $true
        break
    }

    # The container exiting is fatal, and waiting the full timeout out on a dead container helps nobody.
    $state = & $script:DockerPath 'inspect' '--format' '{{.State.Running}}' $ContainerName 2>&1
    if ($LASTEXITCODE -ne 0 -or "$state".Trim() -ne 'true')
    {
        throw "The '$ContainerName' container stopped while starting up. Check docker logs $ContainerName. $probe"
    }

    Start-Sleep -Seconds 1
}

if (-not $ready)
{
    throw "The '$ContainerName' container started but '$Database' did not accept connections within two minutes. Check docker logs $ContainerName."
}

Write-Verbose "Applying $SchemaPath."

# The schema is piped in rather than copied and sourced, so that nothing is left behind inside the container. Raw
# content is used because the client reads a single script, not a line at a time.
$schema = Get-Content -Path $SchemaPath -Raw

$schemaOutput = $schema | & $script:DockerPath `
    'exec' `
    '--interactive' `
    '--env' "PGPASSWORD=$Password" `
    $ContainerName `
    'psql' '--host=127.0.0.1' '--port=5432' '--username' $UserName '--dbname' $Database 2>&1

if ($LASTEXITCODE -ne 0)
{
    throw "The schema could not be applied to '$Database'. $schemaOutput"
}

# Applying the schema is verified rather than assumed, as a client which fails part way through can still exit zero.
$expectedTables = @('idunno_bluesky_correlation_states', 'idunno_bluesky_identities', 'idunno_bluesky_refresh_locks')

$actualTables = & $script:DockerPath `
    'exec' `
    '--env' "PGPASSWORD=$Password" `
    $ContainerName `
    'psql' '--host=127.0.0.1' '--port=5432' '--username' $UserName '--dbname' $Database '--quiet' '--tuples-only' '--no-align' `
    '--command' "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;" 2>&1

if ($LASTEXITCODE -ne 0)
{
    throw "The tables in '$Database' could not be listed. $actualTables"
}

$missingTables = $expectedTables | Where-Object { $_ -notin @($actualTables | ForEach-Object { "$_".Trim() }) }

if ($null -ne $missingTables -and @($missingTables).Count -gt 0)
{
    throw "The schema was applied but '$Database' is missing $($missingTables -join ', '). Check docker logs $ContainerName."
}

Write-Verbose "'$ContainerName' is ready with $(@($expectedTables).Count) tables in '$Database'."

Write-Output "Host=localhost;Port=$Port;Database=$Database;Username=$UserName;Password=$Password;"
