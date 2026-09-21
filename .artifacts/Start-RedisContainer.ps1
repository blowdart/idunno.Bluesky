<#
.SYNOPSIS
Starts a local Redis container for the idunno.Bluesky ASP.NET authentication stores and prints a .NET connection string.

.DESCRIPTION
Checks that Docker is installed and running, starts a Redis container with a password set on the default user, waits for
it to accept authenticated commands, and writes a StackExchange.Redis connection string to the pipeline.

This is a development helper. The container publishes only to the loopback address and the password is passed to
redis-server on its command line, where anything which can inspect the container can read it. Neither is suitable for
anything but a local machine.

.PARAMETER ContainerName
The name to give the container. Defaults to idunno-bluesky-redis.

.PARAMETER Port
The host port to publish Redis on. Defaults to 6379.

.PARAMETER Password
The password to set on the default user. A random one is generated when this is not supplied, so that no credential
needs to live in source control.

.PARAMETER Image
The Redis image to run. Defaults to redis:8-alpine.

.PARAMETER Force
Removes an existing container of the same name rather than failing. Everything in it is lost.

.EXAMPLE
.\Start-RedisContainer.ps1

.EXAMPLE
$connectionString = .\Start-RedisContainer.ps1 -Port 6380 -Force
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ContainerName = 'idunno-bluesky-redis',

    [Parameter()]
    [ValidateRange(1, 65535)]
    [int] $Port = 6379,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Password,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Image = 'redis:8-alpine',

    [Parameter()]
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-RedisPassword
{
    # A connection string is comma delimited with equals separated options, so a password containing either would need
    # escaping by every caller. The alphabet below avoids the problem rather than pushing it downstream.
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

Invoke-Docker -Arguments @('info', '--format', '{{.ServerVersion}}') -FailureMessage 'Docker is installed but is not responding. Start Docker Desktop, or the Docker daemon, and try again.' | Out-Null

if (-not $PSBoundParameters.ContainsKey('Password'))
{
    $Password = New-RedisPassword
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
# offered to the rest of the network.
try
{
    Invoke-Docker `
        -Arguments @(
            'run',
            '--detach',
            '--name', $ContainerName,
            '--publish', "127.0.0.1:${Port}:6379",
            $Image,
            'redis-server', '--requirepass', $Password) `
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

$ready = $false
foreach ($attempt in 1..30)
{
    # REDISCLI_AUTH keeps the password out of the argument list of a process inside the container.
    $ping = & $script:DockerPath 'exec' '--env' "REDISCLI_AUTH=$Password" $ContainerName 'redis-cli' 'ping' 2>&1

    if ($LASTEXITCODE -eq 0 -and "$ping".Trim() -eq 'PONG')
    {
        $ready = $true
        break
    }

    Start-Sleep -Milliseconds 500
}

if (-not $ready)
{
    throw "The '$ContainerName' container started but did not accept an authenticated PING within 15 seconds. Check docker logs $ContainerName."
}

Write-Verbose "'$ContainerName' is ready."

Write-Output "localhost:$Port,user=default,password=$Password"
