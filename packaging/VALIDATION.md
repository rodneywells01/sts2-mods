# Validation — September 5, 2026

Preview 0.1.1 targets Slay the Spire 2 **v0.111.0**, commit `41cef1ea`, Windows x64. It has no BaseLib dependency and needs no PCK: icons and name colors come from the installed game.

## Completed

- Release compilation against the installed `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll`: zero errors/warnings.
- Core checks: 1,014 assertions covering eligibility, locked characters, empty pools, reachable choices, enabled-mode defaults/persistence, persisted exclusions, overwriting settings, and rejecting corrupt/unsupported settings without destroying them.
- Native game loading: manifest detected, initializer invoked, all three Harmony patches installed.
- In-game panel tests: roster populated; all-excluded blocks selection; twenty successive activation events with one allowed character always choose it; manual selection still works; disabling restores the vanilla Random placeholder and re-enabling resolves it through the whitelist.
- Visible game at 1280×720: three consecutive mouse clicks on Random produced Silent, Necrobinder, then Defect without moving focus away. Verified whitelist defaults, icons/colors, keyboard confirmation, help dialog, and native Random with Custom Random off.
- Animation probe: all five native death/power mappings, thirty immediate replacements, eventual hiding, no Include all animation, and unchanged lobby selection. Visible mouse tests confirmed the native character scenes render on the selection screen.
- Local ENet multiplayer, two separate game processes: both peers with this mod, host only, and client only. Each combination connected, agreed on selected characters, and retained them during run initialization.
- Installer under Windows PowerShell 5.1: installation, repeat installation/update, uninstall preserving unrelated files, checksum rejection, version rejection, and paths with spaces. Steam VDF parsing tested with an escaped secondary-library path.

Multiplayer tests used a separate instrumenting `GameProbe` mod on both peers and isolated synthetic profiles. The probe observes state, drives selection/readiness, and unlocks only the synthetic test roster. **GameProbe is not included in this release.** A peer without Random Character Options in these tests still had the probe; these are not a claim of an uninstrumented Steam playthrough.

## Remaining playtest coverage

- A real Steam friend invite/session and a complete multiplayer run, including combat.
- Physical controller hardware, Steam Deck/Linux/macOS, custom character mods, and other UI mods.
- Future game versions. The installer refuses versions other than the tested build.

## Behavioral choice

Vanilla Random resolves on every peer at run start using the same seeded RNG. This mod instead chooses a legal character when Random is pressed and invokes the existing character button. Native `SetLocalCharacter` broadcasts that ordinary choice. No run-generation, networking, or combat methods are patched. Therefore the manifest declares `affects_gameplay: false`; local mixed-install tests support this design. The choice is revealed immediately and is not seed-deterministic.

The Godot headless runs emitted a certificate-store warning and some engine preload/exit warnings. No blacklist exception or multiplayer mismatch was observed in the passing runs. These tests do not establish that the entire game log is warning-free.



