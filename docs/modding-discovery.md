# Modding discovery — September 5, 2026

> Historical initial assessment. For the implemented whitelist, enable toggle, and verified tests, see [implementation](implementation.md) and [validation](../packaging/VALIDATION.md).

## Feasibility

This is a promising small quality-of-life mod. Filtering a list is straightforward; the work is integrating it into the existing Godot screen, saving preferences, and verifying the multiplayer boundary. This assessment is an implementation inference, not a successful runtime test.

## Verified local baseline

Game directory: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2`.

Read-only inspection found:

- `release_info.json`: v0.111.0, commit `41cef1ea`, dated August 13, 2026.
- `data_sts2_windows_x86_64/sts2.runtimeconfig.json`: target `net9.0`, bundled runtime 9.0.7.
- `sts2.dll`, `sts2.xml`, and `0Harmony.dll` are present in that data directory.
- No `mods` folder was present in the game directory. Workshop installations were not inventoried.
- `dotnet --list-sdks` returned no SDKs; `dotnet tool list -g` reported no SDK. This checks the current shell's toolchain, not every possible private installation.
- Workspace was an empty Git repository with no configured remote.

Metadata inspection of the installed DLL, without executing game code, found these useful method names:

| Type | Relevant members |
| --- | --- |
| `NCharacterSelectScreen` | `InitializeMultiplayerAsHost`, `InitializeMultiplayerAsClient`, `SelectCharacter`, `OnLocalCharacterChangedForRandom`, `StartNewMultiplayerRun` |
| `StartRunLobby` | `ChangeCharacter`, `SetLocalCharacter`, `HandleLobbyPlayerChangedCharacterMessage`, `SetReady`, `BeginRunForAllPlayersIfAllReady` |
| `ModManager` | `ReadModsInDirRecursive`, `ReadModManifest`, `TryLoadMod`, `CallModInitializer`, `GetGameplayRelevantModNameList`, `GetNonGameplayRelevantModNameList` |

The XML also exposes `NCharacterSelectButton.IsRandom` and `IsLocked`. These are tracing leads: method bodies, signatures, RNG ownership, and precise patch points remain unverified.

## What creating and loading a mod takes

The maintained template route uses C#, .NET SDK 9 or newer, Mega Crit's MegaDot/Godot tooling, and BaseLib. Its empty-mod template fits this feature better than a new-character template. Publishing builds the DLL and resource pack and copies the files to the game. Code-only changes can subsequently be built without republishing assets. See [template setup](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup).

For our initial prototype, evaluate a smaller C# library referencing the installed game, Godot, and Harmony assemblies. A JSON manifest and DLL can suffice when there are no packaged assets. A mod initializer applies narrowly scoped Harmony patches. We should avoid bundling game assemblies. This lean route is a proposal; the first compile/load test must establish the exact references. The game's shipped Harmony assembly is already available. See [code-only mod structure](https://github.com/TomFrederik/sts_modding/blob/main/MODDING_GUIDE.md).

Expected package:

```text
mods/
  RandomCharacterBlacklist/
    RandomCharacterBlacklist.json
    RandomCharacterBlacklist.dll
    RandomCharacterBlacklist.pck  # only if packaged resources are used
```

The game loads local mods from its `mods` directory. Enable them in Settings → Mod Settings. A manifest identifies the mod, version, DLL/resource presence, dependencies, and gameplay relevance. Modded saves are separate. Local sharing is possible by distributing the mod folder; Workshop publication is a separate step. See [mod files and loading](https://github.com/Alchyr/ModTemplate-StS2/wiki/Modding-Basics).

Verify loading through an explicit initializer log entry and `%APPDATA%\SlayTheSpire2\logs\godot.log`; seeing a manifest in the menu alone does not prove the patch executed. See [debugging guidance](https://github.com/Alchyr/ModTemplate-StS2/wiki/Testing-and-Debugging).

## Multiplayer compatibility decision

The manifest's `affects_gameplay: false` excludes a mod from lobby compatibility checking. Documentation reserves this for mods that do not alter game logic; using it incorrectly can cause desynchronization. A quality-of-life label alone does not establish that qualification. See [manifest semantics](https://github.com/Alchyr/ModTemplate-StS2/wiki/Modding-Basics).

Our design target is a local selection helper: resolve the player's allowed choice locally and submit it through the normal character-selection path. Other players would receive an ordinary character choice. If inspection and testing confirm that boundary, client-only operation may be possible. If Random is resolved by the host at embark, patching only the visual button would be insufficient; we must adapt the design or require installation on all peers. Do not bypass compatibility checks to conceal a real gameplay dependency.

Start testing with matching game builds and identical mod packages/dependencies on both peers. Then test mixed installation explicitly. Do not claim that friends need nothing installed until both host-only and client-only cases succeed.

## Proposed implementation

1. Trace Random from the screen through lobby readiness and run initialization. Identify where the actual character is chosen and whether run RNG is consumed.
2. Store exclusions by stable character ID in user data, outside the recursively scanned manifest directory. Save atomically and recover visibly from corrupt preferences.
3. Add a compact character-select panel with clear exclusion toggles, eligible count, and reset. Preserve mouse and controller navigation and avoid the lobby panel.
4. Compute eligible roster minus exclusions. Respect unlocks and existing availability rules; do not assume a permanent roster of five.
5. Select once from that set. Do not repeatedly reroll, unlock characters, alter another player's preferences, or perturb shared run RNG.
6. With an empty set, retain the current choice and show a useful explanation. Do not silently ignore the blacklist.
7. Pass the result through the verified native selection path; preserve lobby readiness, ascension updates, and duplicate-character rules.

An existing [character-select skin-manager mod](https://github.com/ing-gom/Sts2SkinManager) demonstrates that adding a collapsible panel on this screen is practical. Its presence does not prove our random-selection behavior or network compatibility.

## Acceptance checks before distribution

| Check | Required result |
| --- | --- |
| Clean startup | Manifest loads, initializer logs, patches resolve, no new errors |
| Empty blacklist | Existing eligible roster remains selectable |
| Some excluded | Repeated random choices never select an excluded ID |
| One eligible | Always chooses that character |
| None eligible | Clear message; no forbidden selection or hang |
| Manual choice | Blacklisted character can still be selected manually |
| Persistence | Exclusions survive restart; bad config recovers safely |
| Lobby | Host and client see the same selected character |
| Run start | Actual character matches selection; no desync through first combat |
| Peer configurations | Both modded, only host modded, only client modded; record supported cases |
| Input/lifecycle | Mouse, controller, leave/rejoin, reopening screen, ready/unready |
| Other characters | Locked, removed, newly added, and modded IDs handled predictably |

Local two-instance multiplayer testing is documented using `steam_appid.txt` containing `2868840`, host argument `-fastmp host_standard`, and client argument `-fastmp join`. Additional clients use distinct `-clientId` values. This is a test route to verify against this game build; no test instances were launched in discovery. See [local multiplayer testing](https://github.com/Alchyr/ModTemplate-StS2/wiki/Testing-and-Debugging).

## Distribution

First prepare a ZIP with our mod files, supported game version, dependencies, installation/removal instructions, and tested multiplayer requirements. Only our files belong in the release, not game binaries or personal configuration.

Mega Crit supplies an official [Steam Workshop uploader](https://github.com/megacrit/sts2-mod-uploader). It uses a workspace with `content`, `workshop.json`, and a preview image under 1 MB; `ModUploader.exe upload -w <workspace-folder>` publishes it. Workshop publishing is not required to test locally or share a ZIP with friends. No release or upload has been performed.

