# A fixed save to start every test from (tvnclr fork).
#   .\test-save.ps1 -Snapshot   keeps the current saves as the test point (user\test-save)
#   .\test-save.ps1 -Restore    backs up the current saves (user\save-backups) and puts the test point back
#   .\test-save.ps1             shows what the test point holds
param(
    [switch]$Snapshot,
    [switch]$Restore,
    # inFAMOUS-Dev layout: the play folder sits next to WIP (inFAMOUS-Dev\Jogar).
    [string]$BuildDir = (Join-Path $PSScriptRoot "..\..\..\Jogar")
)

$ErrorActionPreference = "Stop"
$saves = Join-Path $BuildDir "user\home\1000\savedata\CUSA00004"
$point = Join-Path $BuildDir "user\test-save\CUSA00004"
$backups = Join-Path $BuildDir "user\save-backups"

function Describe($folder) {
    Get-ChildItem $folder -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $sfo = Join-Path $_.FullName "sce_sys\param.sfo"
        $text = if (Test-Path $sfo) {
            ([regex]::Matches([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($sfo)), '[^\x00-\x1f]{4,}') |
                ForEach-Object Value | Where-Object { $_ -notmatch '^(PSF|SAVEDATA|ACCOUNT_ID|ATTRIBUTE|CATEGORY|DETAIL|FORMAT|MAINTITLE|PARAMS|SUBTITLE|TITLE_ID|CUSA)' }) -join ' | '
        } else { "" }
        "{0}  {1}  {2}" -f (Get-Item (Join-Path $_.FullName "dis.dat") -ErrorAction SilentlyContinue).LastWriteTime, $_.Name, $text
    }
}

if (Get-Process shadPS4 -ErrorAction SilentlyContinue) {
    throw "Close the game first: it writes its saves while it runs."
}

if ($Snapshot) {
    if (-not (Test-Path $saves)) { throw "No saves in $saves" }
    if (Test-Path $point) { Remove-Item $point -Recurse -Force }
    New-Item -ItemType Directory -Force (Split-Path $point) | Out-Null
    Copy-Item $saves $point -Recurse
    "Test point saved:"
    Describe $point
}
elseif ($Restore) {
    if (-not (Test-Path $point)) { throw "No test point yet: run with -Snapshot first." }
    if (Test-Path $saves) {
        New-Item -ItemType Directory -Force $backups | Out-Null
        $zip = Join-Path $backups ((Get-Date -Format "yyyy-MM-dd HH.mm.ss") + " before test-save.zip")
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::CreateFromDirectory($saves, $zip)
        "Current saves backed up: $zip"
        Remove-Item $saves -Recurse -Force
    }
    Copy-Item $point $saves -Recurse
    "Test point restored:"
    Describe $saves
}
else {
    if (Test-Path $point) { "Test point:"; Describe $point } else { "No test point yet." }
}
