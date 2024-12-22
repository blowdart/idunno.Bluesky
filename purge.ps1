Write-Host 'Always wise to purge the rabble'

$folderNames = 'obj', 'bin', 'CoverageResults', 'TestResult*'

foreach ($folderName in $folderNames) {
    $folders = Get-ChildItem -Path $foldername -recurse
    foreach ($folder in $folders) {
      Write-Host 'Deleting ' $folder.FullName;
      Remove-Item -Path $folder.FullName -recurse -Force
    }
}

$fileNames = 'coverage*.json', 'coverage*.xml', 'coverage*.info', '*.nupkg', '*.snupkg'
foreach ($fileName in $fileNames) {
    $files = Get-ChildItem $fileName -recurse
    foreach ($file in $files)
    {
      Write-Host 'Deleting ' file.FullName;
      Remove-Item -Path $file.FullName -recurse -Force
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

if (Test-Path *.binlog) {
  Write-Host "Deleting binlogs"
  Remove-Item -Path *.binlog -recurse -Force
}

if (Test-Path TestResults) {
  Write-Host "Deleting TestResults"
  Remove-Item -Path TestResults -recurse -Force
}

Write-Host 'Done'
