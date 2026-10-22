# inFAMOUS Second Son Launcher

A small Windows launcher (C# / WPF, .NET 8) that sits next to `shadPS4.exe`: a Home page with Play,
and settings pages for visuals and DLSS, controls, game files and saves, and system options.

It does not change the emulator. It edits the files the emulator already reads at start:

| File | What the launcher writes |
|---|---|
| `shadps4-flags-user.txt` | Its own block at the top (DLSS, render size, mouse, performance mode, ...). Lines below the block are yours and are kept. |
| `user\custom_configs\CUSA00004.json` | Display mode, V-Sync, frame-rate cap, HDR, language, volume, deadzones, motion sensor. |
| `shadps4-game.txt` | The game's `eboot.bin`. |
| `user\launcher.json` | The launcher's own options. |

Before each Play it zips `user\home\1000\savedata\CUSA00004` into `user\save-backups` (last 10 kept).

## Build

Needs the .NET 8 SDK. From this folder:

```powershell
.\publish.ps1                     # -> ..\..\..\Jogar\SecondSonLauncher.exe (inFAMOUS-Dev layout)
.\publish.ps1 -BuildDir D:\Games\SecondSon
```

The exe needs the .NET 8 Desktop Runtime on the PC that runs it. For development,
`SECONDSON_LAUNCHER_ROOT` points the launcher at another folder with `shadPS4.exe`.
