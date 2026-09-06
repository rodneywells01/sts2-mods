# Random Character Options implementation



## Verified integration boundary

Inspection of installed v0.111.0 changed the initial hypothesis. `StartRunLobby.BeginRunLocally` creates a deterministic `act_selection` RNG, selects acts, then resolves each `RandomCharacter` from `ModelDb.AllCharacters`. Every peer performs that operation. A personal blacklist applied there would require synchronization and could change RNG consumption for other players.

The Select prefix suppresses the Random button's focus-triggered selection only while Custom Random is enabled. The panel connects the button's Released event to Mod.Roll, so each mouse/controller activation rolls even when focus remains on Random. The earlier focus-based hook did not reroll on repeated clicks; the activation hook fixes that cause.

Roll chooses from the screen's visible, unlocked, non-random buttons after subtracting saved excluded IDs, then invokes the chosen button's own Select. This follows NCharacterSelectScreen.SelectCharacter → StartRunLobby.SetLocalCharacter and broadcasts the ordinary LobbyPlayerChangedCharacterMessage. No Random placeholder is submitted through the custom path.

The current result can legitimately repeat; the header pulses on every roll. Disabling Custom Random allows the original Select method and skips custom activation behavior, restoring native seeded Random at embark. Enabling with a pending native Random choice immediately resolves it through the whitelist; enabling is blocked if that cannot produce a legal choice. No global roster, run RNG, network message, or teammate selection is changed.

## UI and persistence

The `_Ready` postfix attaches a native Godot panel at the top right. It uses `CharacterModel.IconTexture` and `NameColor`, supports F8/Escape, and routes confirmation only for a control focused inside the panel. The dice header has FocusMode.None: mouse clicks can open it, but mouse/arrow-key focus cannot select it. Native character-row navigation is preserved. The info button exposes hover help and a clickable dialog. Routine explanatory footer text is hidden; blocked/error states remain visible.

Preferences live under `OS.GetUserDataDir()/RandomCharacterBlacklist/preferences.json`, outside the recursive mod-manifest scan. The interface is an inclusion list with every character on by default. Atomic replacement writes schema version 1, Enabled (default true), and excluded stable ModelId strings. Persisting the complement preserves earlier preview settings and includes newly added characters by default. Missing characters remain in preferences but cannot enter the eligible pool. Invalid settings block Random until Include all, preserving the original file. Write failure retains session choices and shows an error.

## Build and package

Run `scripts/Build.ps1`, optionally with `-GamePath <installation>`. It prefers the local `.tools/dotnet` SDK, builds Release, runs core tests, copies only the DLL/manifest and installer documentation, computes SHA-256 hashes, and creates `dist/RandomCharacterBlacklist-0.1.1.zip`.

Development tools used: Microsoft-signed `dotnet-install.ps1`; workspace-local .NET SDK 9.0.317; ILSpyCmd 9.1.0.7988. Tools, game copies, decompiled research, synthetic saves, and build outputs are ignored by Git. No game binaries or decompiled game source belong in the release or repository.

`tests/Installer.Tests.ps1` tests the package against a fixture under `.research`, using Windows PowerShell rather than assuming PowerShell 7 exists on a friend's PC.

## In-game probe reproduction

Build `tests/GameProbe`. Use a separate copy of the game under `.research`; enable this mod and GameProbe only in that copy. GameProbe's manifest has ID `GameProbe`, `has_dll: true`, `has_pck: false`, `affects_gameplay: false`. The probe DLL is a development test dependency, never a release dependency.

Use a separate process `APPDATA` rooted under `.research/appdata`, and an `override.cfg` in each copied game directory:

```ini
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="STS2ModTests/host"
```

Use `STS2ModTests/client` for the client. First boot creates isolated settings; set `mod_settings.mods_enabled` only in those synthetic settings. Disable Steam with `--force-steam off` to avoid Steam Cloud. Verify the startup log's user-data directory before running a probe.

Set `RCB_PROBE_ROLE` to `host` or `client`, `RCB_PROBE_OUTPUT` to a unique JSON output path. Host arguments: `--headless --force-steam off -fastmp host_standard`; client: `--headless --force-steam off -fastmp join -clientId 1000`. Set the client settings under account `default/1000`; the host uses `default/1`. Wait for `COMPLETE` in both result files. To test mixed installation, move only the blacklist manifest outside that copy's mods directory while it is closed. Restore it afterwards. Keep the probe on both peers to observe both sides.

For visual testing, use role `ui`, omit `--headless`, and run windowed at 1280×720. It unlocks the synthetic roster and then leaves the screen interactive.

## Distribution route

The release follows the game's [native local mod format](https://github.com/Alchyr/ModTemplate-StS2/wiki/Modding-Basics). Install.cmd invokes a readable PowerShell installer that discovers Steam libraries, validates the game version and package hashes, and copies two files. It does not install a competing manager or change saves. Existing local-folder mod managers can use the same payload.

Steam Workshop is the long-term automatic-update route. The official [Mega Crit uploader](https://github.com/megacrit/sts2-mod-uploader) accepts the same files under its workspace `content` directory plus Workshop metadata and a preview image. No listing has been created or published. Avoid a duplicate local copy when switching to Workshop later.

See the packaged `VALIDATION.md` for evidence and limits. A real Steam session with Rodney's friend remains the next useful playtest.



## Preview lobby re-entry correction

The synthetic UI probe formerly unlocked/showed the roster only once. The native game recalculates Random visibility on lobby initialization and membership changes: at least one lobby participant must have all characters unlocked. Reopening the lobby therefore hid Random again on the fresh synthetic profile. The UI-only probe now reapplies its synthetic roster after each native visibility refresh; three repeated refresh checks pass. This override is absent from the shipped mod and does not change actual player progression.


