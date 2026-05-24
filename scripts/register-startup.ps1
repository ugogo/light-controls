# Register or remove Light Controls in the current user's Windows startup list.
param(
    [ValidateSet("Enable", "Disable", "Status")]
    [string]$Action = "Enable"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
. (Join-Path $PSScriptRoot "Get-AppExe.ps1")

$ValueName = "LightControls"
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

function Get-RegisteredStartupPath {
    $value = Get-ItemProperty -Path $RegPath -Name $ValueName -ErrorAction SilentlyContinue
    if (-not $value) {
        return $null
    }

    $path = [string]$value.$ValueName
    if ($path.Length -ge 2 -and $path.StartsWith('"') -and $path.EndsWith('"')) {
        return $path.Substring(1, $path.Length - 2)
    }

    return $path
}

function Set-RunAtStartupSetting([bool]$Enabled) {
    $settingsPath = Join-Path $env:LOCALAPPDATA "LightControls\settings.json"
    if (-not (Test-Path $settingsPath)) {
        return
    }

    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($settings.runAtStartup -eq $Enabled) {
        return
    }

    $settings | Add-Member -NotePropertyName runAtStartup -NotePropertyValue $Enabled -Force
    $settings | ConvertTo-Json -Depth 32 | Set-Content $settingsPath -Encoding UTF8
}

switch ($Action) {
    "Status" {
        $registered = Get-RegisteredStartupPath
        if ($registered) {
            Write-Host "Startup enabled: $registered"
        } else {
            Write-Host "Startup disabled."
        }
        break
    }
    "Disable" {
        Remove-ItemProperty -Path $RegPath -Name $ValueName -ErrorAction SilentlyContinue
        Set-RunAtStartupSetting $false
        Write-Host "Removed Light Controls from Windows startup."
        break
    }
    "Enable" {
        $exe = Get-AppExe -Root $Root
        if (-not $exe) {
            throw @"
Light Controls is not built yet. Run one of:
  npm run windows:build
  npm run package
"@
        }

        $resolvedExe = (Resolve-Path $exe).Path
        $quotedExe = if ($resolvedExe.Contains(' ')) { "`"$resolvedExe`"" } else { $resolvedExe }
        Set-ItemProperty -Path $RegPath -Name $ValueName -Value $quotedExe
        Set-RunAtStartupSetting $true
        Write-Host "Registered Light Controls for startup: $resolvedExe"
        break
    }
}
