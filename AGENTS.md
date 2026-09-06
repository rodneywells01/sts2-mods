# Agent guide — STS2 Mods

## Start here

This repository develops Slay the Spire 2 quality-of-life mods. Read `README.md`, `INSTALL.md`, and `docs/implementation.md` before changing installation or game integration. `packaging/VALIDATION.md` describes actual test coverage; `docs/modding-discovery.md` is historical research, not current implementation truth.

Current mod: **Random Character Options**, credited **Built by Rodney Wells (WatersEdge) with GPT-6 Astra.** The stable assembly/folder/manifest ID is `RandomCharacterBlacklist`; preserve it to avoid duplicate installs and lost settings. Target: Windows, game **v0.111.0**, .NET **9**. Check the manifest and project file for the current mod version rather than assuming it.

## Installing for a player

- Follow `INSTALL.md`. Players need only the release ZIP and the game, not an SDK, BaseLib, ILSpy, GameProbe, or another loader.
- Find the intended Steam installation; verify `SlayTheSpire2.exe` and `data_sts2_windows_x86_64/sts2.dll`. Close only the relevant game instance before replacing a loaded DLL.
- Use the packaged installer, for example from the repository root in PowerShell:

  ```powershell
  & './dist/RandomCharacterBlacklist-0.1.2/Install.ps1' -GamePath 'D:\SteamLibrary\steamapps\common\Slay the Spire 2' -NoPause
  ```

  Substitute the actual built package version and verified game folder. `-Uninstall` removes only owned mod files; preferences remain.
- Respect installer version/hash checks. A mismatch requires investigation or a matching build, not removing the guard.
- Installation is successful only after a successful installer result and expected DLL/JSON layout. Distinguish files installed, mod enabled, runtime loaded, and multiplayer tested in your report.
- Do not modify real saves, unlock progression, or mod-loading preferences as a shortcut. Use the game's settings for player activation. Do not redistribute game assemblies or assets.
- Do not install the test `GameProbe` into a player's game. Its `ui` mode overrides unlock/visibility only in synthetic test sessions. Native Random requires at least one lobby participant to have every character unlocked.

## Building and developing

Use PowerShell from the repository root. Install/use a .NET 9 SDK for development. `scripts/Build.ps1` prefers `.tools/dotnet/dotnet.exe` if present, otherwise `dotnet` on PATH. Tool downloads are not required on player machines.

```powershell
./scripts/Build.ps1
# For a non-default Steam library:
./scripts/Build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Slay the Spire 2'
./tests/Installer.Tests.ps1
```

Build references the user's installed `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll` with `Private=false`. No separate Harmony/BaseLib package is required. The build runs core checks and produces `dist/RandomCharacterBlacklist-<version>.zip` with the two-file payload, installer, installation guide, checksums, and validation notes.

Source map:

- `Mod.cs`: initializer and Harmony integration; Random activation and allowed-pool choice.
- `Preferences.cs`: persisted enabled flag/exclusion set and selection logic. UI is an inclusion list; the saved complement means new characters default to included.
- `BlacklistPanel.cs`: native Godot UI, dice icon, input/focus, and settings updates.
- `CharacterPreview.cs`: independent cosmetic character visuals and immediate interruption.
- `packaging/`: Windows PowerShell 5.1-compatible player installer and package documents.
- `tests/CoreTests/`: standalone checks without game runtime.
- `tests/GameProbe/`: isolated in-game test driver, never a release dependency.

## Behavior to preserve

- All characters default to included; manual selection remains unrestricted. Filter custom Random to visible, unlocked, included characters. An empty pool must not select a forbidden character.
- Every Random activation rerolls, even with unchanged focus. The selected character may legitimately repeat; each roll must emit exactly one native character sound and screen shake, including repeats. Use the button's activation/Released event, not focus entry, to trigger rolls.
- Custom Random off restores native selection. Never filter the game's seeded run-start Random resolution independently on each peer: that can desynchronize multiplayer.
- Custom picks use the normal character button and native lobby synchronization. Do not change networking, run generation, or combat for this UI feature. Reassess `affects_gameplay` if scope expands.
- The dice header is clickable and F8 opens the menu, but it has no mouse/arrow-key focus. Preserve native character-row navigation. Closing restores prior valid focus. Keep the dropdown inside screen bounds.
- Individual toggles save immediately, then play death/power visuals directly left of the dropdown. Each new toggle immediately hides/replaces the previous visual. Include all starts no animation. Cosmetic failure must not block settings.
- Respect per-character PowerUp mappings, including Necrobinder `cast_mighty` and Defect `process`. Use installed resources. Hide/cancel previews on panel or scene closure and unsubscribe frame callbacks on exit.
- Preserve atomic preference saves and corrupt-file protection. Keep preferences outside the game's recursive mod-manifest scan.

## Verification

Run checks appropriate to the change. Build/core checks cover logic; installer tests cover fixture install/update/remove, hashes, version checks, and paths with spaces. Do not add tests that merely repeat a trivial text edit.

For game-dependent changes, follow the isolated-copy setup in `docs/implementation.md`. Use separate APPDATA roots, per-copy `override.cfg`, synthetic profiles, and `--force-steam off`. Verify the log's user-data path before proceeding. Never reuse real saves for probes.

After building GameProbe and preparing both copies, `scripts/Test-Integration.ps1` runs both-installed, host-only, and client-only cases through run initialization. Animation/UI checks are documented in `docs/animation-feasibility.md` and implemented by probe roles. Include lobby re-entry/visibility refresh, repeated Random clicks, rapid character toggles, F8/Escape, and panel placement when relevant. Use native computer-use tools for visible-game QA.

Local instrumented ENet tests are not proof of an uninstrumented Steam friend session or full combat run. Preserve that distinction in release notes. Avoid repeatedly rerunning unchanged tests once relevant checks pass.

## Releases and repository hygiene

For on-demand packaging or publishing, use [.agents/skills/publish-sts2-package/SKILL.md](.agents/skills/publish-sts2-package/SKILL.md). Rodney explicitly chose manual releases: do not add CI release workflows, runners, startup tasks, or automatic publishing.

- `INSTALL.md` is the canonical simple installation guide; Build.ps1 copies it into the ZIP. Keep README links, package README, and release notes consistent.
- Keep `.tools`, `.research`, game copies, decompiled code, saves, credentials, and local `project-*.json` records out of Git. Keep DLL artifacts in releases, not source history.
- Check package contents: only this mod's DLL/manifest belong in its payload directory. Never ship GameProbe, game DLLs, or runtime tooling.
- When changing versions, update manifest/project version, initializer/installer/package display strings, release notes, and versioned test paths together.
- Use `codex/` for new development branches. Commit focused changes; preserve unrelated work. Verify the configured Git remote before pushing.
- The GitHub repository and v0.1.2 installer release are public. Inspect actual remote state before publishing; do not infer authorization to change visibility or publish Workshop from a request to edit or push code.
- See `docs/distribution.md` for the official Workshop uploader route. Prepare listing content before seeking any needed publication approval. Avoid duplicate manual/Workshop installations and retain the generated Workshop item ID for updates.

Local AI Project continuity, when available: reuse existing ignored `project-registration.json` and the `register-ai-project` skill. Do not commit local dashboard/task records or create duplicate project identities.
