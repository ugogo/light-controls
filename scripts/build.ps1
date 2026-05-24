$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Stop-AppInstances.ps1")

Stop-LightControlsApp

Push-Location $repoRoot
try {
    dotnet build (Join-Path $repoRoot "LightControls.slnx")
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
