$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet build (Join-Path $repoRoot "LightControls.slnx")
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
