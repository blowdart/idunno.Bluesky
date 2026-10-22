<#
.SYNOPSIS
Starts a local MySQL container for the Bluesky labeler and prints a MySqlConnector connection string.

.DESCRIPTION
Starts MySQL with a named data volume, waits for the configured user and database to accept connections, applies the
labeler MySQL schema, and writes the connection string to the success pipeline. The container is published only to
the loopback address.

An existing named volume is never reset or removed. Reusing one requires the password that was previously configured
for its database user.

.PARAMETER ContainerName
The name to give the container. Defaults to idunno-bluesky-labeler-mysql.

.PARAMETER VolumeName
The Docker named volume for persistent database data. Defaults to idunno-bluesky-labeler-mysql-data.

.PARAMETER Port
The host port to publish MySQL on. Defaults to 3306.

.PARAMETER Database
The database to create and apply the schema to. Defaults to idunno_bluesky_labeler.

.PARAMETER UserName
The non-root user to create. Defaults to idunno_labeler.

.PARAMETER Password
The password for the database user. A random password is generated for a new volume. It must be supplied when
reusing an existing volume.

.PARAMETER Image
The MySQL image to run. Defaults to mysql:8.4.

.PARAMETER SchemaPath
The schema to apply. Defaults to the schema.sql in the idunno.Bluesky.Labeler.MySQL project.

.EXAMPLE
.\Start-MySqlLabelerContainer.ps1

.EXAMPLE
$connectionString = .\Start-MySqlLabelerContainer.ps1 -Port 3307
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]*$')]
    [string] $ContainerName = 'idunno-bluesky-labeler-mysql',

    [Parameter()]
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]*$')]
    [string] $VolumeName = 'idunno-bluesky-labeler-mysql-data',

    [Parameter()]
    [ValidateRange(1, 65535)]
    [int] $Port = 3306,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Database = 'idunno_bluesky_labeler',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $UserName = 'idunno_labeler',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Password,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Image = 'mysql:8.4',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $SchemaPath = (Join-Path $PSScriptRoot '..\src\idunno.Bluesky.Labeler.MySQL\schema.sql')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-LabelerPassword
{
    $alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $bytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    return -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

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

    $output = & $script:DockerPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        throw "$FailureMessage Docker exited with code $LASTEXITCODE."
    }

    return $output
}

if ($null -eq $script:DockerPath)
{
    throw 'Docker is required and must be available on PATH.'
}

if ($UserName -eq 'root')
{
    throw 'MySQL creates root itself, so -UserName cannot be root.'
}

if (-not (Test-Path -LiteralPath $SchemaPath -PathType Leaf))
{
    throw "The labeler schema was not found at '$SchemaPath'. Pass -SchemaPath to specify a schema file."
}

$SchemaPath = (Resolve-Path -LiteralPath $SchemaPath).Path
Invoke-Docker -Arguments @('info', '--format', '{{.ServerVersion}}') -FailureMessage 'Docker is installed but is not responding. Start Docker Desktop and try again.' | Out-Null

$existingContainers = Invoke-Docker `
    -Arguments @('ps', '--all', '--quiet', '--filter', "name=^/$ContainerName$") `
    -FailureMessage 'Existing Docker containers could not be listed.'
if (-not [string]::IsNullOrWhiteSpace(($existingContainers -join '')))
{
    throw "A container named '$ContainerName' already exists. Remove or rename it explicitly before running this script."
}

$existingVolumes = Invoke-Docker `
    -Arguments @('volume', 'ls', '--quiet', '--filter', "name=^$VolumeName$") `
    -FailureMessage 'Docker volumes could not be listed.'
$volumeExists = @($existingVolumes | ForEach-Object { "$_".Trim() } | Where-Object { $_ -eq $VolumeName }).Count -gt 0

if ($volumeExists -and -not $PSBoundParameters.ContainsKey('Password'))
{
    throw "The named volume '$VolumeName' already exists. Supply -Password for its existing database user; the volume will not be reset."
}

if (-not $volumeExists)
{
    Invoke-Docker -Arguments @('volume', 'create', $VolumeName) -FailureMessage "The named volume '$VolumeName' could not be created." | Out-Null
}

if (-not $PSBoundParameters.ContainsKey('Password'))
{
    $Password = New-LabelerPassword
}

if ($volumeExists)
{
    $rootPassword = $Password
}
else
{
    $rootPassword = New-LabelerPassword
}

try
{
    Invoke-Docker `
        -Arguments @(
            'run',
            '--detach',
            '--name', $ContainerName,
            '--publish', "127.0.0.1:${Port}:3306",
            '--volume', "${VolumeName}:/var/lib/mysql",
            '--env', "MYSQL_ROOT_PASSWORD=$rootPassword",
            '--env', 'MYSQL_ROOT_HOST=%',
            '--env', "MYSQL_DATABASE=$Database",
            '--env', "MYSQL_USER=$UserName",
            '--env', "MYSQL_PASSWORD=$Password",
            $Image) `
        -FailureMessage "The '$ContainerName' container could not be started." | Out-Null
}
catch
{
    throw
}

Write-Verbose "Waiting for '$Database' to accept connections as '$UserName'."
$ready = $false
foreach ($attempt in 1..120)
{
    & $script:DockerPath `
        'exec' `
        '--env' "MYSQL_PWD=$Password" `
        $ContainerName `
        'mysql' '--protocol=TCP' '--host=127.0.0.1' '--user' $UserName '--database' $Database '--silent' '--execute' 'SELECT 1' 2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0)
    {
        $ready = $true
        break
    }

    $state = & $script:DockerPath 'inspect' '--format' '{{.State.Running}}' $ContainerName 2>&1
    if ($LASTEXITCODE -ne 0 -or "$state".Trim() -ne 'true')
    {
        throw "The '$ContainerName' container stopped while starting. Check its Docker logs."
    }

    Start-Sleep -Seconds 1
}

if (-not $ready)
{
    throw "The '$ContainerName' container did not accept database connections within two minutes. Check its Docker logs."
}

$schema = Get-Content -LiteralPath $SchemaPath -Raw
$schema | & $script:DockerPath `
    'exec' `
    '--interactive' `
    '--env' "MYSQL_PWD=$Password" `
    $ContainerName `
    'mysql' '--protocol=TCP' '--host=127.0.0.1' '--user' $UserName '--database' $Database 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0)
{
    throw "The labeler schema could not be applied to '$Database'. Check the schema file and Docker logs."
}

$builder = [System.Data.Common.DbConnectionStringBuilder]::new()
$builder['Server'] = '127.0.0.1'
$builder['Port'] = $Port
$builder['Database'] = $Database
$builder['User ID'] = $UserName
$builder['Password'] = $Password

Write-Output $builder.ConnectionString
