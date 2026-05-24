# Create a Desktop shortcut to Light Controls (no bash required).
param(
    [ValidateSet("Desktop", "StartMenu", "Both")]
    [string]$Location = "Desktop",
    [switch]$BuildIfMissing
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$DesktopProject = Join-Path $Root "src\LightControls.Desktop\LightControls.Desktop.csproj"
. (Join-Path $PSScriptRoot "Get-AppExe.ps1")

$exe = Get-AppExe -Root $Root
if (-not $exe -and $BuildIfMissing) {
    Write-Host "Building Light Controls..."
    Push-Location $Root
    try {
        dotnet build $DesktopProject --nologo -v q
    } finally {
        Pop-Location
    }
    $exe = Get-AppExe -Root $Root
}

if (-not $exe) {
    throw @"
Light Controls is not built yet. Run one of:
  npm run windows:build
  npm run package
  pwsh -File scripts/create-shortcut.ps1 -BuildIfMissing
"@
}

$exe = (Resolve-Path $exe).Path
$workDir = Split-Path $exe -Parent
$shortcutName = "Light Controls.lnk"

function New-LightControlsShortcut([string]$Folder) {
    $path = Join-Path $Folder $shortcutName
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $workDir
    $link.Description = "Control RGB lighting through OpenRGB"
    $link.Save()
    Write-Host "Created: $path"
    return $path
}

$created = @()
if ($Location -eq "Desktop" -or $Location -eq "Both") {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $created += New-LightControlsShortcut $desktop
}
if ($Location -eq "StartMenu" -or $Location -eq "Both") {
    $programs = [Environment]::GetFolderPath("Programs")
    $folder = Join-Path $programs "Light Controls"
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $created += New-LightControlsShortcut $folder
}

$created
