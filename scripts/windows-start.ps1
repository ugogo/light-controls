# Build (if needed) and launch Light Controls on Windows.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$DesktopProject = Join-Path $Root "src\LightControls.Desktop\LightControls.Desktop.csproj"
. (Join-Path $PSScriptRoot "Get-AppExe.ps1")
. (Join-Path $PSScriptRoot "Stop-AppInstances.ps1")

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

$resolvedExe = (Resolve-Path $exe).Path
if (Test-LocalLightControlsBuildPath -ExecutablePath $resolvedExe -Root $Root) {
    Stop-LightControlsApp -LocalBuildsOnly -Root $Root
} else {
    Stop-LightControlsApp -ExecutablePath $resolvedExe
}

Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe -Parent)
Write-Host "Started Light Controls from $resolvedExe"
