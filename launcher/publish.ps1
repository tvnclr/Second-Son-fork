# Builds SecondSonLauncher.exe and puts it next to shadPS4.exe.
# Usage: .\publish.ps1 [-BuildDir <folder with shadPS4.exe>]
# Default: the inFAMOUS-Dev play folder (inFAMOUS-Dev\Jogar, next to WIP).
param([string]$BuildDir = (Join-Path $PSScriptRoot "..\..\..\Jogar"))

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "SecondSonLauncher\SecondSonLauncher.csproj"
$staging = Join-Path ([IO.Path]::GetTempPath()) "SecondSonLauncher-publish"

dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $staging -nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $staging "SecondSonLauncher.exe") $BuildDir -Force
Remove-Item $staging -Recurse -Force
Write-Host "SecondSonLauncher.exe -> $BuildDir"
