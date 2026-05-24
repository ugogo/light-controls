function Test-LocalLightControlsBuildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $normalizedRoot = (Resolve-Path $Root).Path.TrimEnd('\')
    $normalizedExe = [System.IO.Path]::GetFullPath($ExecutablePath)

    $localPrefixes = @(
        (Join-Path $normalizedRoot "src\LightControls.Desktop\bin\Debug"),
        (Join-Path $normalizedRoot "src\LightControls.Desktop\bin\Release")
    )

    foreach ($prefix in $localPrefixes) {
        if ($normalizedExe.StartsWith("$prefix\", [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Stop-LightControlsApp {
    param(
        [string]$ExecutablePath,
        [string]$Root,
        [switch]$LocalBuildsOnly
    )

    $processes = @(Get-CimInstance Win32_Process -Filter "Name='LightControls.Desktop.exe'" -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    $toStop = @()
    foreach ($proc in $processes) {
        if (-not $proc.ExecutablePath) {
            continue
        }

        $runningPath = [System.IO.Path]::GetFullPath($proc.ExecutablePath)

        if ($ExecutablePath) {
            $targetPath = [System.IO.Path]::GetFullPath($ExecutablePath)
            if ($runningPath.Equals($targetPath, [StringComparison]::OrdinalIgnoreCase)) {
                $toStop += $proc
            }
            continue
        }

        if ($LocalBuildsOnly) {
            if ($Root -and (Test-LocalLightControlsBuildPath -ExecutablePath $runningPath -Root $Root)) {
                $toStop += $proc
            }
            continue
        }

        $toStop += $proc
    }

    if ($toStop.Count -eq 0) {
        return
    }

    $toStop | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Write-Host "Stopped running Light Controls instance(s)."
}
