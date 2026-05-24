function Stop-LightControlsApp {
    $running = Get-Process -Name "LightControls.Desktop" -ErrorAction SilentlyContinue
    if (-not $running) {
        return
    }

    $running | Stop-Process -Force
    Write-Host "Stopped running Light Controls instance(s)."
}
