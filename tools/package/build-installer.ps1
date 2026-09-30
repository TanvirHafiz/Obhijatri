# Builds the whole installer folder: Setup.exe (prerequisite bootstrapper) + Obhijatri.msix + certificate + README.
# Usage: powershell -File tools\package\build-installer.ps1 [-Out <folder>] [-Pfx <file> -PfxPassword <text>]
param(
    [string]$Out = "$PSScriptRoot\..\..\artifacts\installer",
    [string]$Pfx,
    [string]$PfxPassword = "obhijatri-test"
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path

$packageArgs = @{ Out = $Out; PfxPassword = $PfxPassword }
if ($Pfx) { $packageArgs.Pfx = $Pfx }
& "$PSScriptRoot\build-package.ps1" @packageArgs

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found" }
& $csc /nologo /codepage:65001 /target:exe /platform:x64 /optimize+ /out:"$Out\Setup.exe" /r:System.dll "$PSScriptRoot\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw "Setup.exe build failed" }

# The layout folder is a build leftover, not part of the installer.
if (Test-Path "$Out\layout") { [System.IO.Directory]::Delete("$Out\layout", $true) }
Copy-Item "$PSScriptRoot\README-installer.txt" "$Out\README.txt" -Force

$total = [math]::Round(((Get-ChildItem $Out -File | Measure-Object Length -Sum).Sum) / 1MB, 2)
Write-Host "Installer folder: $Out ($total MB in total)"
