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

**Avalonia 11 (MIT) for the GUI, in new projects, reusing the existing libraries and view models.**

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
| Archive pack (CLI) | yes | **V** `ModTools.Pack` + sync/async extraction pass generated nested fixtures with Kraken | Codex M1a: CLI builds; direct CLI pack invocation not tested |
| CR2W ↔ JSON convert | yes | untested | Codex M1 |
| Kraken compress/decompress | yes | **V** compress levels 1–5 and decompress fixtures 1–6 pass; generated pack/extract at Normal passes | Codex: level 6 encoder aborts; explicit skipped test, see audit below |
| Oodle | optional | A: looks for `liboo2corelinux64.so` | document; Kraken is default |
| Texture import/export (xbm ↔ dds/png/tga) | texconv.dll | **V** eight `Texconv` paths reject non-Windows with a descriptive platform exception; conversion unavailable | Codex M2: native backend + image codecs; audit below |
| Mesh export/import glTF | yes | untested (SharpGLTF is managed) | Codex M2 test |
| Audio (wem/opus) | exe tools + NAudio | partially (libwwtools.so present), opus `.exe` not usable | Codex M3: use system `opus-tools` |
| Scripting (ClearScript V8) | yes | **V** matching Linux x64 native package loads; `ScriptService` executes JS and calls a C# host delegate | Codex: basic runtime fixed in M1a; module loading/debugging remain untested |
| Game detection (Steam, Heroic/GOG, Proton) | registry | none | **Claude M1** |
| GUI shell, docking, settings | WPF | none | **Claude M1–M2** |
| Archive/asset browser | WPF | none | **Claude M1** |
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
| **M1a** Core/CLI baseline | **V locally:** libraries + CLI Release build succeeds; `net10.0` tests: 349 passed, 1 explicit native-crash skip. Generated pack→extract passes (sync + async). Linux workflow added; remote CI and Windows validation pending. Windows TFM/App reference retained. | Codex |
| **M1b** GUI skeleton | `WolvenKit.Linux` Avalonia app starts on X11; detects game installs (Steam, Heroic, manual) with unit tests; opens a real `.archive` and lists its entries; extracts a selected file via native file dialog; headless UI test | Claude |
| **M2a** Native deps | Basic ClearScript execution verified during M1a. Texture conversion, native Kraken level-6 repair, full scripting and native publishing matrix remain open. | Codex |
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
```

## 6. Codex M1a validation and native dependency audit — 2026-11-15

Commands actually run in `wt-codex`, SDK 10.0.403 on CachyOS (the absolute SDK path is shown so these are reproducible without changing PATH):

```bash
/home/kek/.local/share/recovery-toolchain/bin/dotnet build WolvenKit.CLI/WolvenKit.CLI.csproj --configuration Release
# Exit 0: Core, RED4, Common, Modkit, CLI built; 0 warnings / 0 errors on this incremental build.
/home/kek/.local/share/recovery-toolchain/bin/dotnet test Tests/WolvenKit.UnitTests/WolvenKit.UnitTests.csproj --configuration Release --framework net10.0 --logger 'console;verbosity=normal'
# Exit 0: 350 discovered, 349 passed, 1 skipped, 0 failed; 13.0141 seconds.
git diff --check
# Exit 0.
```

The first full library build emitted existing RED4 warnings; the incremental build result does not mean the tree is warning-free. Logs are local at `/tmp/wk-m1a-cli-build.log` and `/tmp/wk-m1a-tests.log`. No game install, Proton prefix or mod project was accessed. New archive fixtures are generated under a unique temporary directory and deleted in `finally`. Existing compression tests now check bytes in memory rather than writing diagnostic output.

Test scope: on Linux the existing UnitTests project targets `net10.0`, omits the WPF App reference and `App/**/*.cs`, and omits Windows texture-conversion tests. All other library tests compile, including the raw-file parser tests; only their `ImportExportHelper` assertion stays Windows-only. App tests (including helpers whose current containing assembly depends on WPF) remain on the Windows target until M3. Windows keeps `net10.0-windows10.0.26100.0`, its App reference, and the full existing source set. This preserves the build configuration by inspection; a Windows run has **not** been performed locally.

Generated archive tests call the real `ModTools.Pack`, `ArchiveReader`, and both `ExtractSingle` variants. They verify three nested filenames, path hashes, uncompressed sizes, actual size reduction for a 64 KiB repetitive JSON payload, uncompressed storage for tiny JSON and `.bin`, byte-for-byte extraction, output count, and pack temporary-file cleanup. They explicitly select Kraken and restore the compression settings afterward. `FileEntry.ShortName` normalizes archive separators only for basename lookup; resource names/hashes retain their archive representation.

| Audit item (Codex) | Evidence and implemented change | Remaining work / recommended fix |
|---|---|---|
| Core `kernel32.dll` | **A:** all Core native-loader callers are in `OodleLib`; its live call sites in `Oodle.Load`, `Compress`, and both `Decompress` overloads already test Windows. Added `SupportedOSPlatform("windows")` to both loader classes so future unguarded use is caught by analyzers. **V:** Linux compression and archive tests never need kernel32. | Keep the existing Windows loader. Contrary to the preliminary Oodle row above, current Core code does not load `liboo2corelinux64.so`; Linux uses Kraken directly. A Linux Oodle implementation would be separate work. |
| `Texconv.cs` | **A:** three metadata methods, two pitch methods and three conversion implementations all reject non-Windows. `ToSaveFormat` is a managed mapping; the ExportArgs overload validates/dispatches to the guarded conversion. **V:** all eight guards now throw a descriptive `PlatformNotSupportedException`, tested without native calls or output writes. Windows branches unchanged. | No Linux texture conversion implemented. Preserve this facade, add a Linux C ABI backend and generated DDS/TGA/BCn tests in M2. |
| Texture dependencies | **A:** `Common.csproj` ships `DirectXTexNet.dll`, mixed-mode `DirectXTexNetImpl.dll`, `Ijwhost.dll`, and `lib/texconv.dll`; `RedImage` calls DirectXTexNet directly and uses WIC for BMP/JPEG/PNG/TIFF. `TexconvNative` also hard-codes the Windows DLL and UTF-16 filenames. | Merely renaming a DLL/import to `.so` is insufficient. The backend needs an explicit ABI (strings, sizes, allocation/free), CPU compression on Linux, and codecs replacing WIC. Route the direct `RedImage` callers as well as `Texconv` through it. Do not claim all image formats from a DDS-only proof. |
| ClearScript | **A:** Modkit had only `Microsoft.ClearScript.V8.Native.win-x64` 7.4.5. Added matching `linux-x64` 7.4.5 on Linux hosts; Windows reference retained. **V:** `ScriptService.ExecuteAsync` runs `report(6 * 7)` and returns 42 through a C# callback, then clears `IsRunning`. | Basic native loading is fixed. ES-module file loading, debugging, cancellation, ARM64, and cross-host publishing remain untested. The package condition currently follows the build host; choose packages by target RID when cross-publishing is introduced. |
| Kraken encoder | **V:** enabling the old tests' compiled-out native compression work exposed a process abort on the existing `oodle.txt` fixture at level 6 (`Optimal2`): `compr_entropy.cpp:707`, `HuffBuilder::BuildCodeLens`, assertion `max_code_len_ <= limit`. Levels 1–5 round-trip, and all six precompressed fixtures decompress. | Level 6 is explicitly inconclusive on Linux so it cannot crash the entire runner; no silent production fallback was added. Repair/rebuild the bundled native encoder before enabling that case. Higher levels were not tested. Normal (4), used by the generated archive test, passes. |
| Existing packer edge cases | **V:** a two-byte `{}` fixture causes `Pack` to return false because `ReadFileInfo` first reads a four-byte magic. A SHA-1 assertion on the generated payload failed. **A:** `ArchiveWriter` computes SHA-1 with the source stream already at EOF, explaining the empty-input digest. Neither is Linux-specific. | Follow up separately with boundary tests for 0–3 byte files and the correct archive SHA-1 semantics (including CR2W buffers). M1a tests verify payload bytes directly; they do not certify SHA-1 metadata or tiny-file support below four bytes. |

Architecture response (Codex): no disagreement with Avalonia, the separate `WolvenKit.Linux` GUI head, or staged multi-targeting of `WolvenKit.App`. The successful library/test build without App demonstrates the existing library boundary is usable. Qualification: changing App's TFM alone is insufficient; its WPF-dependent project references and test types still need the planned platform separation. This task makes no claim about X11/Wayland rendering or one-to-one view reuse. Section 2 still says Avalonia 11; Claude's stated choice of 12.1.6 is owned by M1b and was not changed here.

Publication status: local `git add`/`git commit` failed before any commit was made because Git's worktree metadata is read-only in this execution environment (`.../wolvenkit/.git/worktrees/wt-codex/index.lock`). The code and workflow are prepared in the authorized worktree; pushing, opening the PR, remote CI and cross-review await writable Git metadata. No merge has been attempted.
