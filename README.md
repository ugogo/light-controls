# Light Controls

[![CI](https://github.com/ugogo/light-controls/actions/workflows/ci.yml/badge.svg)](https://github.com/ugogo/light-controls/actions/workflows/ci.yml)

Light Controls is a native Windows RGB lighting app built with WPF and .NET. It combines OpenRGB with direct USB/HID backends so you can control more gear from one place.

## Features

- **OpenRGB devices** — keyboards, RAM, motherboards, and other OpenRGB-compatible hardware.
- **Logitech mice (direct HID++)** — G Pro 2 / PRO X Superlight 2 power LED without OpenRGB. Keeps a persistent session in the tray so firmware idle timeouts do not shut the LED off.
- **Robobloq DX Light bar (direct USB HID)** — monitor light bar control without OpenRGB.
- **Per-device color and brightness** — each detected device remembers its own settings.
- **Built-in swatches + recent custom colors** — quick picks and recently used colors.
- **System tray** — close or minimize hides to the tray; the app keeps running and maintaining device lighting.
- **Resume on startup** — saved colors are re-applied when the app launches.
- **Run at Windows startup** — optional registration so lighting restores after reboot.
- **OpenRGB setup helper** — if the local OpenRGB SDK server is not reachable, the app can find an existing install, download the latest Windows portable build from Codeberg, or launch OpenRGB in server mode.

Settings are stored in `%LocalAppData%\LightControls\settings.json`.

## Requirements

| | Windows |
|---|---------|
| **Run the app** | Windows 10/11, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Build from source** | .NET 10 SDK, [dx-light](https://github.com/ugogo/dx-light) cloned as a sibling repo, Node.js (npm scripts) |
| **Hardware** | OpenRGB-compatible devices, and/or supported Logitech mouse, and/or Robobloq DX Light bar |

For a self-contained build that does not require the .NET runtime, use `npm run package` (see below).

## Quick start

### Windows

```powershell
npm run windows:build
npm start                      # launch app (builds if missing, returns immediately)
npm run windows:shortcut       # Desktop shortcut → Light Controls.lnk
npm run windows:startup        # register to launch when Windows starts
```

Or double-click **Light Controls** on your Desktop after running `windows:shortcut`.

## Build from source

Light Controls depends on [dx-light](https://github.com/ugogo/dx-light) for the DX Light bar backend. Clone it next to this repo:

```text
dev/
  light-controls/
  dx-light/
```

Then build and test:

```powershell
npm run verify
```

CI runs on GitHub Actions (Windows) with the same layout: unit tests plus a Release build of the desktop app. Network integration tests (OpenRGB download checks) are excluded from CI; run them locally with:

```powershell
dotnet test tests/LightControls.Tests/LightControls.Tests.csproj --filter "Category=Integration"
```

## npm scripts

| Script | Description |
|--------|-------------|
| `npm run build` | `dotnet build` the solution |
| `npm start` | Launch the app (builds if missing; terminal returns immediately) |
| `npm run test` | Run unit tests |
| `npm run package` | Publish a self-contained exe to `dist\LightControls\` |
| `npm run launch` | Launch the app (prefers standalone build, else Release) |
| `npm run verify` | Run tests and build |
| `npm run windows:build` | `dotnet build` the solution |
| `npm run windows:shortcut` | Create **Light Controls.lnk** on the Desktop |
| `npm run windows:startup` | Register Light Controls to run when Windows starts |
| `npm run windows:startup:disable` | Remove the Windows startup registration |

## Package (standalone)

```powershell
npm run package
npm run launch
```

The standalone app is written to:

```text
dist\LightControls\LightControls.Desktop.exe
```

After packaging, `npm run launch` uses the self-contained exe. `npm start` and `windows:shortcut` prefer a local Release or Debug build so dev changes are not shadowed by an older `dist` folder.

## Notes

- V1 supports solid colors only.
- OpenRGB is the compatibility layer for most devices; Logitech mouse and DX Light bar use direct HID when enabled.
- No MSIX install, certificate, or Windows package registration is required for the standalone build.
- Some devices may still need OpenRGB-specific permissions or hardware support to appear.
- Logitech G HUB can compete for mouse HID++ control even when mouse lighting is disabled in G HUB. If the mouse LED drops out, try closing G HUB or disabling its background service.
