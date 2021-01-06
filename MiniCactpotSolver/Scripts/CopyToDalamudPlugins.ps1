param (
    [string]$ConfigurationName, 
    [string]$TargetDir,
    [string]$TargetName,
    [string]$SolutionDir
)

if ($ConfigurationName -eq "Release") {
    Write-Output "Copying to DalamudPlugins"
    $prodDir = "$($SolutionDir)..\DalamudPlugins\plugins\"
    $testDir = "$($SolutionDir)..\DalamudPlugins\testing\"
    New-Item -ItemType Directory -Force -Path "$($prodDir)" > $null
    New-Item -ItemType Directory -Force -Path "$($testDir)" > $null
    Copy-Item "$($TargetDir)$($TargetName)" "$($prodDir)" -Force -Recurse
    Copy-Item "$($TargetDir)$($TargetName)" "$($testDir)" -Force -Recurse
}
