function Get-AppExe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $releaseExe = Join-Path $Root "src\LightControls.Desktop\bin\Release\net10.0-windows\LightControls.Desktop.exe"
    $debugExe = Join-Path $Root "src\LightControls.Desktop\bin\Debug\net10.0-windows\LightControls.Desktop.exe"
    $standaloneExe = Join-Path $Root "dist\LightControls\LightControls.Desktop.exe"

    $localBuilds = @($releaseExe, $debugExe) | Where-Object { Test-Path $_ }
    if ($localBuilds.Count -gt 0) {
        return $localBuilds |
            Sort-Object { (Get-Item $_).LastWriteTimeUtc } -Descending |
            Select-Object -First 1
    }

    if (Test-Path $standaloneExe) {
        return $standaloneExe
    }

    return $null
}
