Write-Host 'Always wise to purge the rabble'

$folderNames = 'obj', 'bin', 'CoverageResults', 'TestResult', 'TestResult*'
foreach ($folderName in $folderNames) {
    $folders = Get-ChildItem -Path . -Directory -Recurse -Filter $folderName
    foreach ($folder in $folders) {
        if (Test-Path $folder.FullName)
        {
          Write-Host 'Deleting ' $folder.FullName;
          Remove-Item -Path $folder.FullName -recurse -Force
        }
    }
}

$fileNames = 'coverage*.json', 'coverage*.xml', 'coverage*.info', '*.nupkg', '*.snupkg'
foreach ($fileName in $fileNames) {
    $files = Get-ChildItem -Path . -File -Recurse -Filter $fileName -ErrorAction SilentlyContinue
    foreach ($file in $files)
    {
        if (Test-Path $file.FullName)
        {
          Write-Host 'Deleting ' $file.FullName;
          Remove-Item -Path $file.FullName -recurse -Force
        }
    }
}

if (Test-Path nupkgs) {
  Write-Host "Deleting nupkgs"
  Remove-Item -Path nupkgs -recurse -Force
}

if (Test-Path sign) {
  Write-Host "Deleting sign"
  Remove-Item -Path sign -recurse -Force
}

if (Test-Path sleet) {
  Write-Host "Deleting sleet"
  Remove-Item -Path sleet -recurse -Force
}

if (Test-Path *.binlog) {
  Write-Host "Deleting binlogs"
  Remove-Item -Path *.binlog -recurse -Force
}

if (Test-Path TestResults) {
  Write-Host "Deleting TestResults"
  Remove-Item -Path TestResults -recurse -Force
}

if (Test-Path docs\api) {
  Write-Host "Deleting docfx extracted api documentation"
  Remove-Item -Path docs\api -recurse -Force
}

if (Test-Path docs\_site) {
  Write-Host "Deleting docfx generated site"
  Remove-Item -Path docs\_site -recurse -Force
}

if (Test-Path release-notes.md {
    Write-Host "Deleting generated release-notes.md"
    Remove-Item -Path release-notes.md -Force
}

Write-Host 'Done'
