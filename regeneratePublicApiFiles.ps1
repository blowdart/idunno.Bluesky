$ErrorActionPreference = "Stop" 
$publicApiShippedFileName = 'PublicAPI.Shipped.txt'
$publicApiUnshippedFileName = 'PublicAPI.Unshipped.txt'
$header = "#nullable enable`r`n"
Write-Host 'Regenerating PublicAPI files'

Write-Host 'Gathering directories which have csproj files and PublicAPI* files'
$potentialTargetDirectories = Get-ChildItem -Path . -File -Recurse -Include 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt' | select-object -property DirectoryName -Unique
$targetDirectories = @()
foreach ($targetDirectory in $potentialTargetDirectories) {
    $csprojFiles = Get-ChildItem -Path $targetDirectory.DirectoryName -File -Filter '*.csproj'
    if ($csprojFiles.Count -ne 0) {
        Write-Host 'Adding' $targetDirectory.DirectoryName
        $targetDirectories += $targetDirectory
    }
}

Write-Host 'Resetting PublicAPI files in each target directory'
foreach ($targetDirectory in $targetDirectories) {

    Write-Host $targetDirectory.DirectoryName
    Write-Host $publicApiUnshippedFileName

    $target = Join-Path -Path $targetDirectory.DirectoryName -ChildPath $publicApiUnshippedFileName

    Write-Host 'Resetting' $target
    [System.IO.File]::WriteAllText($target, $header)
    $target = Join-Path -Path $targetDirectory.DirectoryName -ChildPath $publicApiShippedFileName
    Write-Host 'Resetting' $target
    [System.IO.File]::WriteAllText($target, $header)
}

Write-Host 'Running dotnet format analyzers --diagnostics=RS0016 over each project'
foreach ($targetDirectory in $targetDirectories) {
    $csprojFiles = Get-ChildItem -Path $targetDirectory.DirectoryName -File -Filter '*.csproj'
    if ($csprojFiles.Count -gt 0) {
        foreach ($csprojFile in $csprojFiles) {
            Write-Host 'Running dotnet format analyzers on' $csprojFile.FullName
            dotnet format analyzers --diagnostics=RS0016 $csprojFile.FullName
        }
    }  
}

Write-Host 'Promoting PublicAPI.Unshipped.txt into PublicAPI.Shipped.txt'
foreach ($targetDirectory in $targetDirectories)  {
    $unshippedFile = Join-Path -Path $targetDirectory.DirectoryName -ChildPath $publicApiUnshippedFileName
    $shippedFile = Join-Path -Path $targetDirectory.DirectoryName -ChildPath $publicApiShippedFileName
    Write-Host 'Promoting' $unshippedFile ' into ' $shippedFile
    $unshippedContent = [System.IO.File]::ReadAllText($unshippedFile)
    [System.IO.File]::WriteAllText($shippedFile, $unshippedContent)
    [System.IO.File]::WriteAllText($unshippedFile, $header)
}

Write-Host 'Done'
