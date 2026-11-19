# Workspace Session Restorer

A lightweight background tray utility for Windows that automatically captures running application windows and restores their layout after unexpected power cuts or system restarts.

## How It Works

```
               +----------------------------------------------+
               |           Background Tray Agent              |
               |     (Runs on Windows Startup, No Taskbar)    |
               +----------------------+-----------------------+
                                      |
          +---------------------------+---------------------------+
          v                                                       v
+---------------------+                               +---------------------+
| 30s Snapshot Engine |                               | Crash Check Engine  |
+----------+----------+                               +----------+----------+
           |                                                     |
           | Query Top-Level Windows                             | Sentinel-file check
           v                                                     v
+---------------------+                               +---------------------+
|   Win32 Interop     |                               | Show Recovery UI    |
| - EnumWindows       |                               | "Restore N Tasks?"  |
| - GetWindowPlacement|                               +----------+----------+
| - WMI (CommandLine) |                                          | (User clicks "Restore")
+----------+----------+                                          v
           |                                          +---------------------+
           v                                          | Restoration Engine  |
+---------------------+                               | - Launch Process    |
| session.json        |------------------------------>| - SetWindowPlacement|
| (Atomic Disk Write) |                               +---------------------+
+---------------------+
```

### Components

| Component | Description |
|-----------|-------------|
| **Snapshot Engine** | Runs every 30s (configurable 5–300s). Captures visible top-level windows, their positions, sizes, minimized/maximized state, process paths, and full command lines via WMI. Skips cloaked/system windows. |
| **Crash Detector** | Uses a sentinel-file pattern: sentinel deleted while running, written on clean exit. If missing at next startup → unclean shutdown (power cut, crash, forced restart). |
| **Recovery UI** | WPF dialog listing captured applications with checkboxes, process icons/names, window titles, and sizes. User selects which apps to restore. |
| **Restoration Engine** | Launches selected processes, waits for main windows, then applies captured `WINDOWPLACEMENT` via `SetWindowPlacement`. |
| **Tray Agent** | Hardcodet taskbar icon with context menu: Restore Last Session, Settings, Exit. Double-click opens recovery. |

## Features

- **Automatic periodic snapshots** — every 30 seconds (configurable)
- **Atomic disk writes** — session file written via temp-file + `File.Replace` to survive power cuts mid-write
- **Crash detection** — sentinel-file pattern distinguishes clean exits from power cuts / crashes
- **Selective restore** — checkbox list of captured apps; pick only what you need
- **Position + state restore** — normal, maximized, and minimized states via `GetWindowPlacement` / `SetWindowPlacement`
- **Command-line preservation** — relaunches apps with original arguments via WMI `Win32_Process`
- **Auto-start** — optional Startup-folder shortcut (no admin required)
- **Single instance** — named mutex prevents multiple tray icons
- **Filtering** — skips cloaked UWP windows, system shell windows, invisible windows, and the restorer itself

## Requirements

- Windows 10/11 (x64)
- No extra runtime needed — the published app and installer are **self-contained** (bundle the .NET 8 runtime)

## Installation

Run `artifacts\WorkspaceRestorer-Setup-1.0.0.exe`:

- **Per-user install, no admin/UAC** — installed to `%LOCALAPPDATA%\Programs\Workspace Session Restorer`
- Self-contained app exe — no .NET runtime required on the target machine (larger download)
- Setup tasks (all checked by default):
  - **Create a desktop shortcut**
  - **Start with Windows** — creates `WorkspaceRestorer.lnk` in the Startup folder
  - **Start Workspace Session Restorer** — launch app on the Finish page
- Detects a running instance via mutex and asks to close it before upgrading
- **Uninstaller**: Settings → Apps, or `unins000.exe` in the install folder. User data in `%LOCALAPPDATA%\WorkspaceRestorer` is **preserved** on uninstall.

Silent install (tasks use defaults unless overridden):

```powershell
.\WorkspaceRestorer-Setup-1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
# optional task selection:
.\WorkspaceRestorer-Setup-1.0.0.exe /VERYSILENT '/TASKS=desktopicon,startup'
```

Silent uninstall:

```powershell
& "$env:LOCALAPPDATA\Programs\Workspace Session Restorer\uninst\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES
```

## Build

```bash
dotnet build -c Release
```

## Publish (single-file, self-contained)

```bash
dotnet publish src/WorkspaceRestorer/WorkspaceRestorer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Build the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
.\installer\build-installer.ps1    # publish + ISCC -> artifacts\WorkspaceRestorer-Setup-<version>.exe
```

## Run

```bash
dotnet run --project src/WorkspaceRestorer/WorkspaceRestorer.csproj
```

Or launch the published `WorkspaceRestorer.exe`. It will appear in the system tray.

## Data Location

```
%LOCALAPPDATA%\WorkspaceRestorer\
├── session.json         # Latest window snapshot
├── crash_session.json   # Preserved pre-crash session (safeguarded against boot overwrites)
├── sentinel.flag        # Clean-shutdown marker (exists only after clean exit)
├── settings.json        # User preferences
└── restorer.log         # Diagnostic log
```

## Settings

- **Snapshot interval** — seconds between captures (5–300)
- **Start with Windows** — toggle Startup-folder shortcut
- **Skip minimized windows** — exclude minimized windows from capture
- **Restore maximized as maximized** — toggle maximize state restoration
- **Launch delay** — milliseconds between launching each app during restore (default 300ms)

## Limitations

- Cannot restore windows from elevated (admin) processes if the restorer is not elevated
- Some apps (e.g. Electron apps with multi-process models) may need extra time for their main window to appear
- Windows Store / UWP apps are captured but restoration depends on the app's AUMID registration

## License

MIT
