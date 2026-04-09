# catch-catch (Windows)

Multiplayer bongo cat overlay for Windows. Same server as the macOS version.

## Requirements

- Windows 10/11
- .NET 8.0 SDK

## Build & Run

```bash
cd CatchCatch
dotnet run
```

## Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `CatchCatch/bin/Release/net8.0-windows/win-x64/publish/CatchCatch.exe`

## Features

- Transparent overlay with bongo cat on desktop
- Keyboard/mouse activity detection (cat animates when typing)
- 3 cat themes (Gray, White, Calico)
- Multiplayer rooms via WebSocket (same server as macOS)
- Chat with speech bubbles
- System tray icon with settings
- Multi-monitor support
- Draggable cat position
- Auto-update check from GitHub Releases
