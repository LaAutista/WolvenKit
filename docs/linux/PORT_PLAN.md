# WolvenKit native Linux port — plan

Status of every statement here: **V** = verified by running it on Linux, **A** = from reading the code (not yet run).
Test machine: CachyOS, kernel 6.18 LTS, .NET SDK 10.0.403, LXQt on X11. Game: Cyberpunk 2077 (GOG via Heroic, run with Proton).

## 1. Architecture assessment (upstream `main` @ `c6065b22f7`)

| Project | TFM | Role | Linux state |
|---|---|---|---|
| `WolvenKit.Core` | net10.0 | compression (Kraken/Oodle), hashing, logging, Wwise | **V** builds; ships `libkraken.so`, `libwwtools.so`. `NativeMethods.cs` imports `kernel32.dll` (A: needs guard). |
| `WolvenKit.RED4` | net10.0 | CR2W/archive formats, 17.5k generated type files | **V** builds, reads real archives. |
| `WolvenKit.Common` | net10.0 | services, DDS/texture, glTF | **V** builds. Texture conversion calls `lib/texconv.dll` (C++/CLI DirectXTex wrapper, Windows-only); `Texconv.cs` has non-Windows branches that need auditing. References `SharpDX` (managed math types are fine; D3D11 is not). |
| `WolvenKit.Modkit` | net10.0 | import/export, uncook, scripting | **V** builds. `ClearScript.V8.Native.win-x64` only (needs `linux-x64` native); `opus-tools/*.exe`; `NAudio` playback/encoding parts are Windows-only. |
| `WolvenKit.CLI` | net10.0 | command line | **V** builds; `unbundle` extracted 53/53 entries from `basegame_2_mainmenu.archive`. Other commands untested. |
| `WolvenKit.App` | net10.0-windows | view models, app services (450 files) | Does not build on Linux. 104 files reference WPF/Win32/Helix/SharpDX types (`System.Windows.*`, `Microsoft.Win32`, `HelixToolkit.Wpf.SharpDX`, `System.Drawing`, NAudio). The other ~346 are plain MVVM (CommunityToolkit.Mvvm). |
| `WolvenKit` | net10.0-windows | WPF views (144 XAML) | Windows-only by construction: WPF, Syncfusion WPF (commercial licence), AdonisUI, HandyControl, MahApps, AvalonEdit, Nodify (WPF), Helix/SharpDX (Direct3D 11), WebView2. |
| `WolvenKit.Unpacker` | net10.0-windows | installer helper | Not needed on Linux. |
| Tests | UnitTests/IntegrationTests/UITests are `net10.0-windows`; FunctionalTests/Utility are `net10.0` | | Unit tests cannot run on Linux until retargeted. |

Upstream Linux history: issues #316, #1989, #2329, #2996, #694, #903, #2657 — all about the CLI or about running the WPF app under Wine. No Avalonia or native GUI branch exists upstream.

## 2. Chosen stack

**Avalonia 12 (MIT, 12.1.6) for the GUI, in new projects, reusing the existing libraries and view models.**

Why, compared with the alternatives:

| Option | Verdict |
|---|---|
| Avalonia | XAML + bindings + styles map almost 1:1 from WPF, works with CommunityToolkit.Mvvm and ReactiveUI already used here, native X11 and Wayland (XWayland today, native Wayland backend in progress), xdg-desktop-portal file dialogs, MIT. Direct ports exist for the main WPF dependencies: AvaloniaEdit (AvalonEdit), Dock.Avalonia (docking), Nodify.Avalonia / NodifyAvalonia (graph editor), TreeDataGrid/DataGrid (Syncfusion grids). **Chosen.** |
| Uno Platform | WinUI XAML dialect, so the 144 WPF views port less directly; heavier toolchain. |
| GTK#/Gir.Core, Qt bindings | No XAML reuse, would be a rewrite of the view layer and bindings. |
| Wine/Proton for the WPF app | Excluded by the project goal. |
| Blazor/Electron-style web UI | Throws away all views and controls. |

Structure:

```
WolvenKit.Core / RED4 / Common / Modkit     unchanged TFM (net10.0), Linux fixes behind OS checks
WolvenKit.App                               multi-target net10.0 + net10.0-windows; WPF-typed files move behind
                                            small interfaces or are Windows-only compiled   (phase 3-4, joint)
WolvenKit.Linux        (new, net10.0)       Avalonia head: views, platform services (dialogs, game detection)
Tests/WolvenKit.Linux.Tests (new, net10.0)  xunit + Avalonia.Headless
```

The Windows WPF app stays untouched and keeps building from the same tree, so upstream merges are mostly conflict-free: Linux code is additive, and the only shared edits are portability fixes that are upstreamable on their own.

3D viewport (Helix/SharpDX → Direct3D 11) needs a real replacement: OpenGL via Avalonia `OpenGlControlBase` + Silk.NET is the planned route (phase 4). Texture conversion needs a Linux-native DirectXTex build (it supports CMake/Linux) behind the existing `Texconv` API.

## 3. Compatibility matrix

| Feature | Windows | Linux now | Plan |
|---|---|---|---|
| Archive read / extract (CLI) | yes | **V** works | regression test with generated fixture |
| Archive pack (CLI) | yes | untested | Codex M1: round-trip test |
| CR2W ↔ JSON convert | yes | untested | Codex M1 |
| Kraken compress/decompress | yes | **V** decompress works (extract) | compress in round-trip test |
| Oodle | optional | A: looks for `liboo2corelinux64.so` | document; Kraken is default |
| Texture import/export (xbm ↔ dds/png/tga) | texconv.dll | **broken** (A) | Codex M2: native DirectXTex wrapper `.so` |
| Mesh export/import glTF | yes | untested (SharpGLTF is managed) | Codex M2 test |
| Audio (wem/opus) | exe tools + NAudio | partially (libwwtools.so present), opus `.exe` not usable | Codex M3: use system `opus-tools` |
| Scripting (ClearScript V8) | yes | **broken**, win-x64 native only | Codex M2: add `ClearScript.V8.Native.linux-x64` |
| Game detection (Steam native/Flatpak/Snap, Heroic GOG/Epic, manual folder) | registry | **V** works (`GameLocator`, unit tests; real Heroic install found). Lutris/Bottles not covered yet | Claude M2: Lutris |
| GUI shell | WPF | **V** Avalonia window starts on X11 (LXQt). Wayland untested. No docking/settings yet | Claude M2 |
| Archive browser | WPF | **V** per-archive file list, filter, extract selection (refuses to write into the game folder). No cross-archive tree/search yet | Claude M2 |
| Project explorer, new/open project, pack & install | WPF | none | Claude M2 |
| Red file (CR2W) tree editor | WPF + Syncfusion | none | Claude M3 |
| Text/script/tweak editor | AvalonEdit | none | Claude M3 (AvaloniaEdit) |
| Import/export tool UI | WPF | none | Claude M3 |
| Graph editors (quest/scene) | Nodify WPF | none | M4 |
| 3D mesh/entity/sector preview | Helix + D3D11 | none | M4 (OpenGL) |
| Audio player, video (bink) | NAudio / external | none | M4 |
| Installer/update | Inno/WinSparkle-style | none | Codex M5: PKGBUILD, AUR |
| Discord RPC, plugins manager (RED4ext, CET… install into game dir) | yes | none | M4; must write to the Proton-run game dir safely |

## 4. Roadmap and owners

| Milestone | Acceptance (must be demonstrated on Linux) | Owner |
|---|---|---|
| **M1a** Core/CLI baseline | Linux CI job builds libs + CLI and runs tests on `net10.0`; pack→extract round-trip test with generated fixture passes; unit tests that do not need WPF run on Linux; Windows build unaffected | Codex |
| **M1b** GUI skeleton | `WolvenKit.Linux` Avalonia app starts on X11; detects game installs (Steam, Heroic, manual) with unit tests; opens a real `.archive` and lists its entries; extracts a selected file via native file dialog; headless UI test | Claude |
| **M2a** Native deps | texture conversion and ClearScript work on Linux with tests | Codex |
| **M2b** Projects | create/open `.cpmodproj`, project explorer, add files from archive, pack, install to game dir | Claude |
| **M3** Editors | `WolvenKit.App` multi-targeted; CR2W tree editor, text editor, import/export UI | joint (Codex: App multi-target; Claude: views) |
| **M4** Parity | graph editors, 3D preview, audio, plugins | split after M3 review |
| **M5** Packaging | `PKGBUILD`, clean-chroot build, desktop file, icons, AUR evaluation | Codex |
| **M6** Upstream | upstreamable portability PRs prepared; documented upstream-merge procedure | joint |

Each milestone ends with a cross-review PR into `linux/main` and a note in the coordination issue.

## 5. Verified commands

```bash
dotnet build WolvenKit.CLI/WolvenKit.CLI.csproj                       # 0 errors (2026-11-15)
dotnet WolvenKit.CLI/bin/Debug/net10.0/WolvenKit.CLI.dll unbundle <game>/archive/pc/content/basegame_2_mainmenu.archive -o <tmp>
                                                                      # Unbundled 53/53 entries
dotnet test Tests/WolvenKit.Linux.Tests/WolvenKit.Linux.Tests.csproj  # Passed 7/7 (2026-11-15): game detection, headless window, pack->list->extract round trip
dotnet run --project WolvenKit.Linux -- <game>/archive/pc/content/basegame_2_mainmenu.archive
                                                                      # window shows Heroic install, its archive list (57 archives on disk), 53 files (screenshot taken on X11)
```

Not verified yet for M1b: native file dialogs and the Extract button were not clicked by hand (the extract code path is covered by the headless test); Wayland session.
