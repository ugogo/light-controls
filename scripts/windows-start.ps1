# Build (if needed) and launch Light Controls on Windows.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$DesktopProject = Join-Path $Root "src\LightControls.Desktop\LightControls.Desktop.csproj"
$StandaloneExe = Join-Path $Root "dist\LightControls\LightControls.Desktop.exe"
$ReleaseExe = Join-Path $Root "src\LightControls.Desktop\bin\Release\net10.0-windows\LightControls.Desktop.exe"
$DebugExe = Join-Path $Root "src\LightControls.Desktop\bin\Debug\net10.0-windows\LightControls.Desktop.exe"

function Get-AppExe {
    if (Test-Path $ReleaseExe) { return $ReleaseExe }
    if (Test-Path $DebugExe) { return $DebugExe }
    if (Test-Path $StandaloneExe) { return $StandaloneExe }
    return $null
}

$exe = Get-AppExe
if (-not $exe) {
    Write-Host "Building Light Controls (Release)..."
    Push-Location $Root
    try {
        dotnet build $DesktopProject -c Release --nologo -v q
    } finally {
        Pop-Location
    }
    $exe = Get-AppExe
    if (-not $exe) {
        throw "App executable not found after build. Expected: $ReleaseExe"
    }
}

$running = Get-Process -Name "LightControls.Desktop" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Light Controls is already running."
    exit 0
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent)
Write-Host "Started Light Controls."
