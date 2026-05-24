$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
. (Join-Path $PSScriptRoot "Get-AppExe.ps1")

$exe = Get-AppExe -Root $Root
if (-not $exe) {
    throw "Standalone app not found. Run npm run package first."
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent)
