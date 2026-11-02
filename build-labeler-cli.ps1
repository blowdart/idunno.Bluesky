param(
    [ValidateSet("win-x64", "win-arm64", "linux-x64", "linux-arm64")]
    [string[]] $RuntimeIdentifier = @("win-x64", "win-arm64", "linux-x64", "linux-arm64")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$sourceRevisionId = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceRevisionId)) {
    throw "Unable to determine the source commit ID for the CLI build."
}

$project = Join-Path $repositoryRoot "src\idunno.Bluesky.Labeler.Cli\idunno.Bluesky.Labeler.Cli.csproj"
$artifactRoot = Join-Path $repositoryRoot ".assets\labeler-cli"

foreach ($rid in $RuntimeIdentifier) {
    $output = Join-Path $artifactRoot $rid
    $null = New-Item -ItemType Directory -Force -Path $output

    Write-Host "Publishing trimmed self-contained CLI for $rid to $output"
    & dotnet publish $project `
        --configuration Release `
        --framework net10.0 `
        --runtime $rid `
        --self-contained true `
        --property:PublishTrimmed=true `
        --property:PublishSingleFile=true `
        --property:BuildCommitId=$sourceRevisionId `
        --output $output

    if ($LASTEXITCODE -ne 0) {
        throw "CLI publish failed for runtime identifier '$rid' with exit code $LASTEXITCODE."
    }
}
