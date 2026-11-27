<#
  Builds the extension and packs it into a .pext file in the "dist" folder.

  Requirements: .NET SDK (https://dotnet.microsoft.com/download) - any recent version.
  Visual Studio is NOT required.

  Usage (PowerShell, in this folder):
      .\build.ps1             # build + pack
      .\build.ps1 -Install    # build + pack + open the .pext so Playnite installs it
#>
param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = (Get-ChildItem (Join-Path $root "src") -Filter *.csproj | Select-Object -First 1).FullName
$outDir = Join-Path $root "src\bin\Release"
$dist = Join-Path $root "dist"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet was not found. Install the .NET SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

Write-Host "Building..." -ForegroundColor Cyan
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }

# Read version from extension.yaml
$manifest = Get-Content (Join-Path $root "src\extension.yaml")
$idLine = $manifest | Where-Object { $_ -match '^Id:\s*(.+)$' } | Select-Object -First 1
$id = ($idLine -replace '^Id:\s*', '').Trim()
$verLine = $manifest | Where-Object { $_ -match '^Version:\s*(.+)$' } | Select-Object -First 1
$version = ($verLine -replace '^Version:\s*', '').Trim()

# Files that go into the package (Playnite.SDK.dll is provided by Playnite itself)
$files = Get-ChildItem $outDir -File | Where-Object {
    $_.Name -ne "Playnite.SDK.dll" -and $_.Extension -notin @(".pdb", ".xml")
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$pext = Join-Path $dist ("{0}_{1}.pext" -f $id, ($version -replace '\.', '_'))
$zip = [System.IO.Path]::ChangeExtension($pext, ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $pext) { Remove-Item $pext -Force }

Compress-Archive -Path $files.FullName -DestinationPath $zip
Move-Item $zip $pext

Write-Host ""
Write-Host "Package created: $pext" -ForegroundColor Green
Write-Host ($files.Name -join ", ")

if ($Install) {
    Write-Host "Opening package, Playnite will ask to install it..." -ForegroundColor Cyan
    Start-Process $pext
}
