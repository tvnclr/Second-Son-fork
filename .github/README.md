<!--
SPDX-FileCopyrightText: 2026 tvnclr
SPDX-License-Identifier: GPL-2.0-or-later

The front page of github.com/tvnclr/shadPS4 (branch second-son-dev). axylpup's original front page is .github/UPSTREAM.md.
-->

<div align="center">

# inFAMOUS Second Son on PC · tvnclr's fork

**A personal fork of [axylpup's Second Son build](https://github.com/axylpup/shadPS4) of the
[shadPS4](https://github.com/shadps4-emu/shadPS4) PlayStation 4 emulator.**<br>
Smooth scene changes with a self-built EXE, a desktop launcher, and a PGO workflow of its own.

[![Branch](https://img.shields.io/badge/branch-second--son--dev-e0452b?style=for-the-badge)](https://github.com/tvnclr/shadPS4/tree/second-son-dev)
[![Based on](https://img.shields.io/badge/based%20on-second--son%20v1.1-555?style=for-the-badge)](https://github.com/axylpup/shadPS4/releases/tag/second-son-v1.1)
[![License](https://img.shields.io/badge/license-GPL--2.0--or--later-555?style=for-the-badge)](../LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-555?style=for-the-badge)](#building)

**[What's new](#whats-new) · [Results](#results) · [Launcher](#secondsonlauncher) · [Building](#building) ·
[Own PGO profile](#an-own-pgo-profile) · [Troubleshooting](#troubleshooting) · [Credits](#credits)**

</div>

> [!NOTE]
> Unofficial and experimental, like the build it is based on. Not affiliated with Sony Interactive Entertainment,
> Sucker Punch Productions, the shadPS4 team or axylpup. **You need your own copy of the game**; no game files and no
> NVIDIA files are included. Tested with the EU version (CUSA00004) and update 1.07.

## What's new

Everything else is axylpup's v1.1: features, controls, DLSS and the FAQ are on the
[upstream front page](UPSTREAM.md).

| Change | What it does | Where |
|---|---|---|
| **Shader index reads the update** | The index of the game's shader code now lays the update folder (`CUSA00004-patch` / `-UPDATE`) over the base game, as the game itself reads them. Upstream read the base folder only, so with the update kept beside the game none of the prepared shaders were found and every shader compiled during play. | `src/video_core/shader_scan.cpp`, `renderer_vulkan/vk_pipeline_precompile.cpp` |
| **The release's shader cache with any EXE** | `SHADPS4_SEED_ANY_BUILD=1` lets an EXE compiled by someone else use the shader seed shipped with the release. Upstream accepts it only from the exact EXE that verified it. | `renderer_vulkan/vk_pipeline_serialization.cpp` |
| **PGO training on a whole session** | `SHADPS4_PGO_SESSION=1` makes an instrumented build record the entire play session (written every minute) instead of one two-minute window. | `src/common/fault_monitor.cpp` |
| **SecondSonLauncher** | A desktop launcher: Play, settings pages, game-file checks, save backups. | [`launcher/`](../launcher/README.md) |
| **Fixed test save** | One save to start every test from, so measurements compare like with like. | `scripts/test-save.ps1` |

Both new flags are off by default: without them the fork behaves as upstream.

## Results

Measured on a Ryzen 7 9800X3D + RTX 4060 (8 GB), EU copy with update 1.07 in a separate `-patch` folder, the release's
shader cache and `SHADPS4_SEED_ANY_BUILD=1`:

| Shader preparation at start | Upstream v1.1 | This fork |
|---|---:|---:|
| Files indexed | 1,779 (base only) | **1,873** (base + update) |
| Shaders found in the game's files | 8,887 | **10,374** |
| Prepared shaders whose code was found | 0 of 9,896 | **9,896 of 9,896** |
| Pipelines ready before play | 775 | **~19,100** |

In play, the hitches when a scene cuts or the area changes are gone: those shaders are now built on the loading
screen instead of mid-frame.

## SecondSonLauncher

A C# / WPF (.NET 8) launcher that sits next to `shadPS4.exe`. It does not change the emulator: it edits the files
the emulator already reads at start (its own block in `shadps4-flags-user.txt`, `user\custom_configs\CUSA00004.json`,
`shadps4-game.txt`) and starts the game.

- **Home**: the game's own artwork (read from your dump), the render size, display mode, DLSS and frame cap at a
  glance, and Play.
- **Visuals**: display mode, V-Sync (including half refresh), frame-rate cap, HDR, render size with the resulting
  resolution for each choice (worked out exactly as the emulator does), screen shape, HUD, performance mode, DLSS.
- **Controls**: mouse aiming and speed, button icons, stick deadzones, touchpad on pads without one, motion sensor.
- **Game & Saves**: game folder checks (eboot.bin, update 1.07, zero-filled files, a DLC over `sce_sys`), language,
  volume, a save backup before every Play (last 10 kept) and restore.
- **System**: graphics card, compatibility renderer, start the game directly (hold Shift for the launcher),
  shader cache, logs, desktop shortcut.

Build and install it with `launcher\publish.ps1`; details in [launcher/README.md](../launcher/README.md).

## Building

Requirements:

- **Visual Studio 2022** with *Desktop development with C++*, *C++ Clang Compiler for Windows* and
  *MSBuild support for LLVM* (the MSVC libraries and headers).
- **LLVM 23** (`winget install LLVM.LLVM`) for the optimized builds. The release's PGO profile is format version 13,
  which needs LLVM 22 or newer; Visual Studio's clang 19 cannot read it.
- **.NET 8 SDK** for the launcher.
- Git, with submodules: `git clone --recursive -b second-son-dev https://github.com/tvnclr/shadPS4`

From a *x64 Native Tools* prompt (or after `vcvars64.bat`):

```bat
:: Plain Release build (Visual Studio's clang)
cmake --preset x64-Clang-Release
cmake --build Build\x64-Clang-Release --parallel

:: Optimized like the official EXE: RelWithDebInfo + auto-inline + ThinLTO + PGO, with LLVM 23
set LLVM=C:/Program Files/LLVM/bin
cmake -S . -B Build\x64-Clang-RelWithDebInfo -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo ^
  -DCMAKE_C_COMPILER="%LLVM%/clang-cl.exe" -DCMAKE_CXX_COMPILER="%LLVM%/clang-cl.exe" ^
  -DCMAKE_LINKER="%LLVM%/lld-link.exe" -DCMAKE_AR="%LLVM%/llvm-lib.exe" -DHAVE_ENDIAN_H=0 ^
  -DASTRA_AUTO_INLINE=ON -DASTRA_THINLTO=ON -DASTRA_PGO_USE_FILE=%CD%/pgo/second-son-v1.1.profdata
cmake --build Build\x64-Clang-RelWithDebInfo --parallel
```

> [!IMPORTANT]
> With LLVM 23 pass `-DHAVE_ENDIAN_H=0`. LLVM 23 ships its own `endian.h`, and libressl then takes a code path that
> has no byte-swap functions on Windows (`call to undeclared function 'be32toh'`).

The EXE runs like the release: put it in a folder with the release's `shadps4-flags.txt`, `defaults\` and
`tex_replace\` (they are not in the repository), `dlss\` for DLSS, and the release's `user\cache` for the prepared
shaders. Add `SHADPS4_SEED_ANY_BUILD=1` to `shadps4-flags-user.txt` so a self-built EXE uses that cache.

## An own PGO profile

The release's profile was recorded with the author's compiler. Built with LLVM 23 only about 72% of it matches; the
rest, including the hottest renderer functions (`vk_a2.cpp`, `liverpool.cpp`), is discarded as *hash mismatch*. A
profile recorded with your own compiler and code matches completely:

1. **Training build**: the same options with `-DASTRA_PGO_GENERATE_DIR=<folder>` instead of `-DASTRA_PGO_USE_FILE`,
   in its own build folder.
2. **Play** with `SHADPS4_PGO_SESSION=1` in `shadps4-flags-user.txt`: open city, fights with powers, smoke and fire,
   rooftops, scene changes. The first three minutes (boot, shader warm-up) are skipped; after that the counters go to
   `pgo-session-<pid>.profraw` next to the EXE every minute. Several sessions are fine.
3. **Merge**: `llvm-profdata merge -o pgo\<name>.profdata pgo-session-*.profraw`
4. **Final build**: the optimized build with `-DASTRA_PGO_USE_FILE` pointing at the new profile.

Re-train after larger code changes: the profile follows the code it was recorded with.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| The game closes a few seconds after start; `fatal.txt` shows an access violation at `eboot.bin+0x309e33` | Game files extracted with **PKG Extractor 1.0**, which writes zeros for parts of packages over 2 GB | Extract the PKGs again with [PKG Extractor 1.1](https://github.com/AzaharPlus/shadPS4Plus/releases/tag/PKG_EXTRACTOR_1_1) or newer. The launcher's *Full check* lists empty files. |
| `param.sfo` shows a DLC (`UNLOCK...`) and the game misbehaves | A DLC package was extracted into the base game folder and replaced 11 `sce_sys` files | Extract the base game again into an empty folder; DLCs go to their own folder. |
| Hitches at scene changes on the first play | The prepared shaders are not used | Keep the update as `CUSA00004-patch` / `-UPDATE` (this fork indexes it), copy the release's `user\cache`, and set `SHADPS4_SEED_ANY_BUILD=1`. The log line `Preloaded N pipelines` should show about 19,000. |
| `unsupported instrumentation profile format version` | The PGO profile is newer than the compiler | Build with LLVM 22 or newer. |

## Credits

- **[axylpup](https://github.com/axylpup/shadPS4)**: the inFAMOUS Second Son build this fork is based on (v1.1),
  including the shader seed, the shader index, the PGO setup and everything on the
  [upstream front page](UPSTREAM.md).
- **[shadPS4](https://github.com/shadps4-emu/shadPS4)** and its contributors: the emulator.
- **[AzaharPlus](https://github.com/AzaharPlus/shadPS4Plus)**: PKG Extractor.
- **NVIDIA**: Streamline and DLSS (not included; download them from NVIDIA).

Licensed under the [GNU General Public License v2.0 or later](../LICENSE), as upstream. inFAMOUS and Second Son are
trademarks of Sony Interactive Entertainment.
