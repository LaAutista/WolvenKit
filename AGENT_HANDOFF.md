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

### 2026-11-15 — PR #3 Windows guard fixed; PR #2 re-review has one blocker
- Branch `linux/codex/m1-foundations`, HEAD `55550564d192e15ab529d4fb2dafc3f60324c9df` (PR #3). Claude committed the previous work. This follow-up is deliberately uncommitted for Claude; no Git write/commit/push attempted.
- Changed `CompressionTests.Kraken_Compress` to return outside Linux, restoring the previous Windows no-op while retaining Linux level 1–5 round trips and the known level-6 skip. Recorded the actual Windows CI `0xC0000005` stack and the removed `_WINDOWS` guard in plan §7. The native Windows encoder itself is not repaired; updated Windows CI remains pending.
- Verified: `/home/kek/.local/share/recovery-toolchain/bin/dotnet test Tests/WolvenKit.UnitTests/WolvenKit.UnitTests.csproj --configuration Release --framework net10.0 --logger 'console;verbosity=normal'` — exit 0, **349 passed, 1 skipped, 0 failed**, 350 total, 13.1415 seconds. Log: `/tmp/wk-m1a-windows-guard-tests.log`. `git diff --check` — exit 0.
- PR #2 re-review target: `d89762f8c4867abe03973d176aff1197142ad5c5`. Read the five changed files and relevant callers/tests from Git; did not edit Claude's worktree. Ran `/home/kek/.local/share/recovery-toolchain/bin/dotnet test WolvenKit.Linux.sln` in an exact `git archive` snapshot at `/tmp/wk-pr2-d89762f8c4-k7j9h4q2` — exit 0, **18 passed, 0 failed, 0 skipped**. Log: `/tmp/wk-pr2-d89762f8c4-tests.log`. Existing full-build warnings plus a `File.SetUnixFileMode` platform warning were emitted; no warning-free build claim.
- **PR #2 blocker:** `WolvenKit.Linux/Services/SafePaths.cs:17` recursively calls `RealPath` without a hop limit or cycle detection. `ResolveLinkTarget(true)` does not catch a cycle through an intermediate directory in the returned target. A temp-home `.local/share/Steam -> .local/share/Steam/child` link makes `GameLocator.FindInstalls(home)` recurse until process-aborting stack overflow, instead of a caught IOException. This is independent of the acknowledged validation/write TOCTOU window. Bound the resolution or track visited links and throw IOException on a cycle; add regression coverage.
- Reproduction uses exact unmodified `SafePaths.cs` and `GameLocator.cs` from that commit in `/tmp/wk-pr2-path-probe`. `/home/kek/.local/share/recovery-toolchain/bin/dotnet build /tmp/wk-pr2-path-probe/Probe.csproj --no-restore` — exit 0. With core dumps disabled, `timeout 10s /home/kek/.local/share/recovery-toolchain/bin/dotnet /tmp/wk-pr2-path-probe/bin/Debug/net10.0/Probe.dll discoverycycle` — exit **134**, `Stack overflow`, repeated `SafePaths.RealPath`, then `GameLocator.TryRealPath` / `FindInstalls`. Log: `/tmp/wk-pr2-discovery-cycle.log`. Ordinary traversal/symlink destination refusals and output-alias resolution also behaved as intended in the isolated probe.
- No real game, Proton prefix, or mod project used; all review fixtures/output are temporary. Dialog/Extract-button UI behavior and Wayland remain untested. Next: Claude commits the PR #3 fix and reruns Windows CI; fixes PR #2's cycle handling before approval.

### 2026-11-15 — M1a implementation ready; Git metadata blocks publication
- Branch `linux/codex/m1-foundations`, base/HEAD `787e2ab1bf`; no new commits yet. Local Git metadata is read-only: `git add` and `git commit` fail creating `../wolvenkit/.git/worktrees/wt-codex/index.lock`. No push or PR yet.
- Implemented: conditional Linux `net10.0` unit tests; generated Kraken pack/extract tests (sync and async); `FileEntry.ShortName` separator fix; analyzer annotations for the Windows-only loader; Linux ClearScript native dependency and execution test; explicit unsupported texture-backend errors; additive `.github/workflows/linux-core-tests.yml` for `linux/**` pushes/PR targets.
- Verified with `/home/kek/.local/share/recovery-toolchain/bin/dotnet` (10.0.403): `build WolvenKit.CLI/WolvenKit.CLI.csproj --configuration Release` exited 0, all five projects built, 0 warnings/0 errors on the incremental build. First full build had existing RED4 warnings.
- Verified: `test Tests/WolvenKit.UnitTests/WolvenKit.UnitTests.csproj --configuration Release --framework net10.0 --logger 'console;verbosity=normal'` exited 0: **349 passed, 1 skipped, 0 failed** (350 total, 13.0141 s). `git diff --check` exited 0. Logs: `/tmp/wk-m1a-tests.log`, `/tmp/wk-m1a-cli-build.log`.
- The explicit skip is a demonstrated native abort in `libkraken.so` compression level 6 (`HuffBuilder::BuildCodeLens`, `compr_entropy.cpp:707`). Levels 1–5 compress/decompress and levels 1–6 fixture decompression passed. Default Normal (4) generated archive round trips passed.
- Open: Linux texture implementation; WPF App tests until M3; Windows execution and remote CI pending; existing packer rejection of <4-byte files and SHA-1-at-EOF metadata issue documented in plan §6. ClearScript basic execution works; modules/debugging/cross-host publishing untested.
- Architecture: no disagreement with Avalonia/new Linux head/staged App multi-targeting. A TFM change alone cannot isolate App's WPF dependencies. No GUI runtime claim. Leave Avalonia version edits to Claude.
- No game/prefix/project access, package installation, or edits outside the Codex worktree (apart from normal NuGet cache and temporary files). New fixtures are generated and deleted under temp directories.
- Next: enable writes to this worktree's shared Git metadata, create focused commits with the required Codex trailer, push only `origin/linux/codex/m1-foundations`, open a PR against fork `linux/main`, run remote CI and request Claude cross-review. No merge without cross-review.
