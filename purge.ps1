Write-Host '🗑️ Always wise to purge the rabble'

# Delete nested folders
$folderNames = 'obj', 'bin', 'CoverageResults', 'TestResult', 'TestResult*'
foreach ($folderName in $folderNames) {
    $folders = Get-ChildItem -Path . -Directory -Recurse -Filter $folderName
    foreach ($folder in $folders) {
        if (Test-Path $folder.FullName)
        {
          Write-Host '📂 Deleting ' $folder.FullName;
          Remove-Item -Path $folder.FullName -recurse -Force
        }
    }
}

# Delete directories off root
$folderNames = 'codecoverage', 'coveragereport', 'nupkgs', 'sign', 'sleet', 'TestResults'
foreach ($folderName in $folderNames) {
    $folders = Get-ChildItem -Path . -Directory -Filter $folderName
    foreach ($folder in $folders) {
        if (Test-Path $folder.FullName)
        {
          Write-Host '📂 Deleting ' $folder.FullName;
          Remove-Item -Path $folder.FullName -recurse -Force
        }
    }
}

if (Test-Path 'docs')
{
    # Delete docs generated directories
    $folderNames = 'api', '_site'
    foreach ($folderName in $folderNames) {
        $folders = Get-ChildItem -Path docs -Directory -Filter $folderName
        foreach ($folder in $folders) {
            if (Test-Path $folder.FullName)
            {
              Write-Host '📂 Deleting ' $folder.FullName;
              Remove-Item -Path $folder.FullName -recurse -Force
            }
        }
    }
}

if (Test-Path *.binlog) {
  Write-Host "📄 Deleting binlogs"
  Remove-Item -Path *.binlog -recurse -Force
}

if (Test-Path release-notes.md) {
    Write-Host "📄 Deleting generated release-notes.md"
    Remove-Item -Path release-notes.md -Force
}

Write-Host '✅ Done'
