# Light Controls

Light Controls is a native Windows RGB lighting app built with WPF and .NET.

- If the local OpenRGB SDK server is not reachable, the app can find an existing OpenRGB install, download the latest Windows portable build from Codeberg, or launch OpenRGB in server mode.

## Features

- Detects OpenRGB-compatible RGB devices.
- Lets you select which devices should receive updates.
- Applies one solid color to selected devices.
- Saves basic named color presets.
- Persists host, port, OpenRGB path, selected devices, last color, and presets to `%LocalAppData%\LightControls\settings.json`.
- Downloads and installs OpenRGB into `%LocalAppData%\LightControls\OpenRGB` when you click **Download & set up**.

## Requirements

| | Windows |
|---|---------|
| **Run the app** | Windows 10/11, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Build from source** | .NET 10 SDK, Node.js (npm scripts) |
| **Hardware** | OpenRGB-compatible RGB devices |

For a self-contained build that does not require the .NET runtime, use `npm run package` (see below).

## Quick start

### Windows

```powershell
npm run windows:build
npm run windows:start          # launch app (builds Release if missing)
npm run windows:shortcut       # Desktop shortcut → Light Controls.lnk
```

Or double-click **Light Controls** on your Desktop after running `windows:shortcut`.

### Developer run

```powershell
npm start
```

## npm scripts

| Script | Description |
|--------|-------------|
| `npm run build` | `dotnet build` the solution |
| `npm start` | Run the desktop app from source (requires SDK) |
| `npm run test` | Run unit tests |
| `npm run package` | Publish a self-contained exe to `dist\LightControls\` |
| `npm run launch` | Launch the app (prefers standalone build, else Release) |
| `npm run verify` | Run tests and build |
| `npm run windows:build` | `dotnet build` the solution |
| `npm run windows:start` | Launch the app (PowerShell; builds Release if missing) |
| `npm run windows:shortcut` | Create **Light Controls.lnk** on the Desktop |

## Package (standalone)

```powershell
npm run package
npm run launch
```

The standalone app is written to:

```text
dist\LightControls\LightControls.Desktop.exe
```

After packaging, `npm run launch` uses the self-contained exe. `windows:start` and `windows:shortcut` prefer a local Release or Debug build so dev changes are not shadowed by an older `dist` folder.

## Notes

- V1 supports solid colors only.
- OpenRGB is used as the hardware compatibility layer.
- No MSIX install, certificate, or Windows package registration is required for the standalone build.
- Some devices may still need OpenRGB-specific permissions or hardware support to appear.
