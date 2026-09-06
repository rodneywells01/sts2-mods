# Validation — September 6, 2026

Preview **0.1.4**, prepared locally for review, targets Slay the Spire 2 **v0.111.0**, commit `41cef1ea`, Windows x64. No BaseLib or PCK is required. This document distinguishes automated local tests from player reports.

## Completed for this change

- Release build against the installed game assemblies: zero warnings/errors.
- **1,019 core checks**: eligibility, locks, empty pools, reachable choices, atomic saves, enabled/reveal persistence, legacy migration, and invalid settings preserved for recovery.
- Native loading and panel creation with all four Harmony patches.
- Immediate mode: twenty rolls with one eligible character, exactly one character sound and screen shake per roll including repeats, manual excluded picks, disabled native Random, and re-enabling with a pending Random.
- Lock-in mode: twenty Random activations retain the native mystery placeholder. Empty-pool Embark leaves the player unready; restoring an eligible character recovers. Switching a pending mystery to immediate reveals a legal pick. Manual picks remain unrestricted. Singleplayer lock-in preserves the included choice through run initialization. Keeping options open from Random focus cannot undo the resolved choice; failed lock-in also restores an Embark button disabled by the tutorial callback.
- Local ENet multiplayer with **both installed, host only, and client only** in both reveal modes. All completed with consistent character choices through run initialization.
- Additional **host-only and client-only** cases with the unmodded teammate choosing native Random: both peers produced identical final player/character rosters, while the modded player retained the allowed character. Character selection and Ready use the existing Reliable transport; no networking/run-generation patches were added.
- Visible native desktop test at **1280×720**: F8 opens the expanded panel, Escape closes it, Random retains its mystery screen, Ready reveals the included character, and Unready retains that concrete pick.
- Rendered layout checks at **1280×720, 1920×1080, 2560×1440, and 3840×2160**: panel stays within screen bounds after resizing. Dice texture is 232×160 for a 58×40 logical icon; the private panel font uses MSDF. Keyboard Enter selects a reveal mode without readying the player. Include all preserves reveal mode. Three native visibility refreshes retain Random in the synthetic test roster.
- All five death/power animation mappings, thirty rapid replacements, eventual hiding, no Include all animation, and unchanged lobby selection passed again.

- Windows PowerShell 5.1 installer fixtures passed install/update, owned-file removal preserving unrelated files, tamper rejection, game-version rejection, Steam path parsing, and paths with spaces. ZIP entry/hash/version verification passed for the final local package.

## Reproduce

Build the mod with `scripts/Build.ps1` and build `tests/GameProbe` using the local .NET SDK. Prepare isolated host/client copies as described in `docs/implementation.md`, then run:

```powershell
./tests/Installer.Tests.ps1
./scripts/Test-Integration.ps1 -Mode immediate
./scripts/Test-Integration.ps1 -Mode lock-in
./scripts/Test-Integration.ps1 -Mode lock-in -Cases host-only,client-only -NativePeerRandom
./scripts/Test-Ui.ps1 -Role singleplayer
./scripts/Test-Ui.ps1 -Role layout
./scripts/Test-Ui.ps1 -Role animation
```

All game probes use synthetic saves, isolated APPDATA, and `--force-steam off`. The probe checks its resolved user-data path before touching synthetic progress. GameProbe remains on both peers even in mixed-mod tests and is **never included in the package**. Sanitized check results are in `docs/test-results/reveal-0.1.4.json`.

## Player report and remaining coverage

On September 6, Rodney reported that the prior mod worked for him during a real Steam multiplayer match with an unmodded friend. This supports the previous behavior in that session; it does not establish the new lock-in mode, a completed full run, or all combinations of mods.

The new mode still needs a Steam friend-session playtest. Physical controllers, Steam Deck/Linux/macOS, custom characters, other UI mods, a complete combat run, and future game versions remain unverified. The installer retains its game-version guard.

## Behavioral boundary

**Immediately** rolls on Random activation. **At lock-in** rolls at the local player's Embark/Ready press, and the result can then be seen by the player and lobby while waiting for teammates. Both modes use a fresh local roll and send an ordinary character choice before readiness. They do not reproduce native seed determinism. **Custom Random off** leaves the game's exact seeded Random resolver intact and suspends the inclusion filter.

No run-generation, networking, or combat methods are patched; `affects_gameplay` remains false. Headless runs can emit the previously observed certificate-store and engine preload/exit warnings; passing probes are not a claim that every game log is warning-free.
