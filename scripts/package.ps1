$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "dist\LightControls"
. (Join-Path $PSScriptRoot "Stop-AppInstances.ps1")

Stop-LightControlsApp

if (Test-Path -LiteralPath $publishDir) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item -LiteralPath $publishDir -Recurse -Force
            break
        } catch {
            if ($attempt -eq 5) {
                throw
            }

            Start-Sleep -Milliseconds 500
        }
    }
}

dotnet publish "$repoRoot\src\LightControls.Desktop\LightControls.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

Write-Host "Standalone app published to $publishDir\LightControls.Desktop.exe"
