$folders = 'obj', 'bin'

foreach ($folder in $folders) {
    Get-ChildItem -Path $folder -recurse | Remove-Item -Path {$_.FullName} -recurse -Force 
}

Remove-Item -Path nupkgs -recurse -Force
Remove-Item -Path sign -recurse -Force
