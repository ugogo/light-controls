# Build (if needed) and launch Light Controls on Windows.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$DesktopProject = Join-Path $Root "src\LightControls.Desktop\LightControls.Desktop.csproj"
. (Join-Path $PSScriptRoot "Get-AppExe.ps1")

$exe = Get-AppExe -Root $Root
if (-not $exe) {
    Write-Host "Building Light Controls..."
    Push-Location $Root
    try {
        dotnet build $DesktopProject --nologo -v q
    } finally {
        Pop-Location
    }
    $exe = Get-AppExe -Root $Root
    if (-not $exe) {
        throw "App executable not found after build."
    }
}

$running = Get-Process -Name "LightControls.Desktop" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Light Controls is already running. Close it and run this again to pick up the latest build."
    exit 0
}

$resolvedExe = (Resolve-Path $exe).Path
Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe -Parent)
Write-Host "Started Light Controls from $resolvedExe"
