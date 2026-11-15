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
- Working on: M1b (Avalonia skeleton, game detection, archive browser).

## Codex

_(Codex: add your entries here.)_
