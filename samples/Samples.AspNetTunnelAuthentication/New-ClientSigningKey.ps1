#Requires -Version 7.4

<#
.SYNOPSIS
Creates an ES256 (ECDSA P-256) key pair for OAuth confidential-client authentication.

.DESCRIPTION
Writes the private key as a PKCS#8 PEM file and the public key as a JSON Web Key (JWK)
to a .blueskyDotnet folder in the current user's profile directory.
The JWK key ID (kid) is the RFC 7638 SHA-256 thumbprint of the public key.
Existing keys are not overwritten unless -Force is specified.
Use -NewKey to create a uniquely named key pair alongside existing keys for rotation.

.PARAMETER OutputDirectory
The folder to write the key files to. Defaults to .blueskyDotnet in the user's profile directory.

.PARAMETER Force
Overwrite existing key files.

.PARAMETER NewKey
Create a uniquely named key pair without replacing existing keys. Cannot be combined with -Force.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.blueskyDotnet'),
    [switch] $Force,
    [switch] $NewKey
)

$ErrorActionPreference = 'Stop'

if ($NewKey -and $Force) {
    throw "Use either -NewKey or -Force, not both."
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$keyName = if ($NewKey) { "client-signing-key-$([Guid]::NewGuid().ToString('N'))" } else { 'client-signing-key' }
$privateKeyPath = Join-Path $OutputDirectory "$keyName.pem"
$publicKeyPath = Join-Path $OutputDirectory "$keyName.jwk.json"

if (-not $Force -and ((Test-Path $privateKeyPath) -or (Test-Path $publicKeyPath))) {
    throw "A client signing key already exists in $OutputDirectory. Use -NewKey to create another key, or -Force to replace it."
}

$additionalKeyPaths = @(
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        Get-ChildItem -LiteralPath $OutputDirectory -Filter 'client-signing-key*.pem' -File |
            Where-Object { $_.FullName -ne $privateKeyPath } |
            Sort-Object Name |
            Select-Object -ExpandProperty FullName
    }
)

function ConvertTo-Base64Url([byte[]] $bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function ConvertTo-ConfigurationPath([string] $path) {
    $profileDirectory = [Environment]::GetFolderPath('UserProfile')
    $relativePath = [System.IO.Path]::GetRelativePath($profileDirectory, $path)
    if (-not [System.IO.Path]::IsPathRooted($relativePath) -and
        $relativePath -ne '..' -and
        -not $relativePath.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)") -and
        -not $relativePath.StartsWith("..$([System.IO.Path]::AltDirectorySeparatorChar)")) {
        return "~/$($relativePath.Replace('\', '/'))"
    }

    return $path
}

function Set-PrivateKeyPermissions([string] $path) {
    if ([System.OperatingSystem]::IsWindows()) {
        $fileInfo = [System.IO.FileInfo]::new($path)
        $acl = [System.IO.FileSystemAclExtensions]::GetAccessControl(
            $fileInfo,
            [System.Security.AccessControl.AccessControlSections]::Access)
        $acl.SetAccessRuleProtection($true, $false)
        $accessRules = $acl.GetAccessRules(
            $true,
            $true,
            [System.Security.Principal.SecurityIdentifier])
        foreach ($accessRule in $accessRules) {
            $acl.RemoveAccessRuleSpecific($accessRule)
        }

        $currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
        $ownerAccessRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $currentUserSid,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($ownerAccessRule)
        [System.IO.FileSystemAclExtensions]::SetAccessControl($fileInfo, $acl)
    }
    else {
        $ownerOnlyMode = [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite
        [System.IO.File]::SetUnixFileMode($path, $ownerOnlyMode)
    }
}

function Write-PrivateKey([string] $path, [string] $contents, [System.Text.Encoding] $encoding) {
    if (-not [System.IO.File]::Exists($path)) {
        $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
        $stream.Dispose()
    }

    Set-PrivateKeyPermissions $path
    [System.IO.File]::WriteAllText($path, $contents, $encoding)
}

$null = New-Item -ItemType Directory -Path $OutputDirectory -Force

$key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $parameters = $key.ExportParameters($false)
    $x = ConvertTo-Base64Url $parameters.Q.X
    $y = ConvertTo-Base64Url $parameters.Q.Y

    # RFC 7638 thumbprint: required members only, in lexicographic order, with no whitespace.
    $thumbprintInput = "{`"crv`":`"P-256`",`"kty`":`"EC`",`"x`":`"$x`",`"y`":`"$y`"}"
    $kid = ConvertTo-Base64Url ([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($thumbprintInput)))

    $jwk = [ordered]@{
        kty = 'EC'
        crv = 'P-256'
        x   = $x
        y   = $y
        kid = $kid
        alg = 'ES256'
        use = 'sig'
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    Write-PrivateKey $privateKeyPath $key.ExportPkcs8PrivateKeyPem() $utf8NoBom
    [System.IO.File]::WriteAllText($publicKeyPath, ($jwk | ConvertTo-Json), $utf8NoBom)
}
finally {
    $key.Dispose()
}

Write-Host "Private key: $privateKeyPath"
Write-Host "Public JWK:  $publicKeyPath"
Write-Host "Key ID:      $kid"
Write-Host ''
Write-Host 'Merge these settings into BlueskyAgent:OAuthOptions in appsettings.json:'
Write-Host ([ordered]@{
    ClientSigningKeyPath = ConvertTo-ConfigurationPath $privateKeyPath
    AdditionalClientSigningKeyPaths = @($additionalKeyPaths | ForEach-Object { ConvertTo-ConfigurationPath $_ })
} | ConvertTo-Json -Depth 3)
Write-Host ''
Write-Host 'Additional paths include other client-signing-key*.pem files in this directory. Review the list before using it.'
Write-Host 'Keep ClientSigningKeyId unset to use the thumbprint shown above.'
if ($additionalKeyPaths.Count -gt 0) {
    Write-Host 'Before promoting the new key, publish it by adding its path to AdditionalClientSigningKeyPaths while keeping the current active key.'
    Write-Host 'Then use the settings above to promote the new key, retaining previous keys until their sessions expire.'
}
Write-Host 'Restart the application after each configuration change, or redeploy if configuration is packaged with it.'
