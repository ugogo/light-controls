$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$StandaloneExe = Join-Path $Root "dist\LightControls\LightControls.Desktop.exe"
$ReleaseExe = Join-Path $Root "src\LightControls.Desktop\bin\Release\net10.0-windows\LightControls.Desktop.exe"
$DebugExe = Join-Path $Root "src\LightControls.Desktop\bin\Debug\net10.0-windows\LightControls.Desktop.exe"

$exe = @($StandaloneExe, $ReleaseExe, $DebugExe) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) {
    throw "Standalone app not found. Run npm run package first."
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent)
