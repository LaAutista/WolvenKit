# Agent handoff — WolvenKit Linux port

Coordination thread: https://github.com/LaAutista/WolvenKit/issues/1
Rules: `AGENTS.md` · Plan: `docs/linux/PORT_PLAN.md`

Each agent edits only its own section, newest entry first.

## Shared state

| Item | Value |
|---|---|
| Fork | https://github.com/LaAutista/WolvenKit |
| Upstream base | `main` @ `c6065b22f7` |
| Integration branch | `linux/main` |
| Workspace | `/home/kek/Documents/VibeCoding/WolvenKit-Linux/` (`wolvenkit` main clone, `wt-claude`, `wt-codex`) |
| Game install used for manual tests (read-only!) | `/home/kek/Games/Heroic/Cyberpunk 2077` (GOG via Heroic) |

## Claude

### 2026-11-15
- Branch `linux/claude/m1-foundations`.
- Done: fork, worktrees, `AGENTS.md`, `PORT_PLAN.md`, this file, coordination issue #1.
- Verified: CLI builds on Linux; `unbundle` extracts 53/53 from `basegame_2_mainmenu.archive`.
- M1b implemented: `WolvenKit.Linux` (Avalonia 12.1.6) with `GameLocator`, `ArchiveService`, `MainViewModel`, `MainWindow`; `Tests/WolvenKit.Linux.Tests` (xunit + Avalonia.Headless); `WolvenKit.Linux.sln`.
- Verified: `dotnet test Tests/WolvenKit.Linux.Tests` 7/7; app launched on X11 against the real install and listed `basegame_2_mainmenu.archive` (53 files).
- Not verified: file dialogs / Extract button by hand, Wayland.
- Found: `FileEntry.ShortName` returns the full path on Linux (backslash names); `ArchiveWriter` skips non-RED extensions and stores `.bin` uncompressed (matters for fixtures); Avalonia 12 headless xunit adapter needs xunit v3, so tests use `HeadlessUnitTestSession` directly with the repo's xunit 2.9.3.
- Next: M2b (projects, cross-archive asset browser, settings, docking).

## Codex

_(Codex: add your entries here.)_
