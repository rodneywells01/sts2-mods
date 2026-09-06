# Random Character Options implementation



## Verified integration boundary

Inspection of installed v0.111.0 changed the initial hypothesis. `StartRunLobby.BeginRunLocally` creates a deterministic `act_selection` RNG, selects acts, then resolves each `RandomCharacter` from `ModelDb.AllCharacters`. Every peer performs that operation. A personal blacklist applied there would require synchronization and could change RNG consumption for other players.

In immediate mode, the Select prefix suppresses the Random button's focus-triggered selection while Custom Random is enabled. The panel connects the button's Released event to Mod.Roll, so each mouse/controller activation rolls even when focus remains on Random. In lock-in mode, native Select is allowed and Released does not roll, leaving the ordinary Random placeholder visible and synchronized.

Roll chooses from the screen's visible, unlocked, non-random buttons after subtracting saved excluded IDs, then invokes the chosen button's own Select. This follows NCharacterSelectScreen.SelectCharacter → StartRunLobby.SetLocalCharacter and broadcasts the ordinary LobbyPlayerChangedCharacterMessage.

In lock-in mode, a prefix on `NCharacterSelectScreen.OnEmbarkPressed` resolves a pending Random just before the original handler disables controls and calls SetReady. The first-time tutorial prompt is allowed to complete first. A failed/empty/corrupt-settings roll cancels the handler, leaving the player unready with controls usable. The current inclusion list is evaluated at lock-in, not cached when Random is selected. Character and Ready messages both use the existing Reliable transport; no message definition, networking method, or run-start resolver is patched. This keeps mixed installations compatible. The result becomes visible at the local player's lock-in, possibly before teammates are ready. It is deliberately a fresh roll, not a recreation of the native seeded resolver. Unready retains the concrete selection; another Random selection arms a new mystery. Manual concrete selections remain unrestricted.

The current result can legitimately repeat; the header pulses on every roll. Because native Select skips an already-selected button, repeated custom picks explicitly replay CharacterSelectSfx and the native weak/short 90-degree screen shake; changed picks retain native feedback without duplication. Disabling Custom Random restores native seeded Random at embark. Enabling or switching to immediate mode with a pending Random resolves it through the inclusion list, or blocks the setting change if that cannot produce a legal choice. Switching to lock-in preserves a concrete/manual selection until Random is selected again. No global roster, run RNG, network message, or teammate selection is changed. The manifest remains `affects_gameplay: false`: both modes submit an ordinary character before readiness, and do not change run generation or combat.

## UI and persistence

The dice header owns its full rounded background and padding, so its entire visible surface handles clicks. The layout root is transparent and ignores mouse input; the options dropdown owns a separate padded panel. The expanded dropdown separates activation, reveal timing, and character inclusion. Its text explains the selected mode; filters are visibly disabled while Custom Random is off. Include all preserves reveal timing. The panel duplicates the fallback font and enables MSDF glyph rendering on that private copy, and the dice SVG is rasterized at 4× (232×160) while displaying at the same 58×40 logical size with linear filtering. The old 58×40 raster could soften when scaled up. FitPanel bounds the entire panel to the screen and recalculates after content or resolution changes; the roster scrolls. No global viewport/render-scale setting is changed.

The `_Ready` postfix attaches a native Godot panel at the top right. It uses `CharacterModel.IconTexture` and `NameColor`, supports F8/Escape, and routes confirmation only for a control focused inside the panel. The dice header has FocusMode.None: mouse clicks can open it, but mouse/arrow-key focus cannot select it. Native character-row navigation is preserved. The info button exposes hover help and a clickable dialog. Routine explanatory footer text is hidden; blocked/error states remain visible.

Preferences live under `OS.GetUserDataDir()/RandomCharacterBlacklist/preferences.json`, outside the recursive mod-manifest scan. Atomic replacement writes schema version 1, Enabled (default true), Reveal (0 = immediately, 1 = at lock-in), and excluded stable ModelId strings. The optional Reveal field defaults to immediate for both fresh and legacy settings. Unknown values are rejected without overwriting the original. Persisting the complement of the inclusion list preserves earlier choices and includes newly added characters by default. Missing characters cannot enter the eligible pool. Invalid settings block custom rolls until Include all explicitly resets them. Write failure retains session choices and shows an error.

## Build and package

Run `scripts/Build.ps1`, optionally with `-GamePath <installation>`. It prefers the local `.tools/dotnet` SDK, builds Release, runs core tests, copies only the DLL/manifest and installer documentation, computes SHA-256 hashes, and creates `dist/RandomCharacterBlacklist-0.1.4.zip`.

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

See the packaged `VALIDATION.md` for evidence and limits. On September 6, Rodney reported a successful Steam match with an unmodded friend on the prior version. Lock-in mode still needs that real-session player check.



## Preview lobby re-entry correction

The synthetic UI probe formerly unlocked/showed the roster only once. The native game recalculates Random visibility on lobby initialization and membership changes: at least one lobby participant must have all characters unlocked. Reopening the lobby therefore hid Random again on the fresh synthetic profile. The UI-only probe now reapplies its synthetic roster after each native visibility refresh; three repeated refresh checks pass. This override is absent from the shipped mod and does not change actual player progression.
