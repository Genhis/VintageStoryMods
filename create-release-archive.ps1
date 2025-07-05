param([string]$projectDir, [string]$version)

$project = Split-Path -Path $projectDir -Leaf
$tmp = "$projectDir/bin/tmp/"
mkdir $tmp > $null

robocopy "$projectDir/bin/Release/Mods/mod/" $tmp > $null
robocopy "$projectDir/assets/" "$tmp/assets/" /e > $null
Copy-Item "$projectDir/changelog.md" $tmp
Copy-Item "$projectDir/readme.md" $tmp
Copy-Item "$projectDir/../license.txt" $tmp

$destination = "$projectDir/bin/$($project)_$version.zip"
Echo "$project -> $destination"
Get-ChildItem -Path $tmp | Compress-Archive -Force -DestinationPath $destination
rmdir $tmp -recurse
