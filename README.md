# Slay the Spire 2 Mods

Create, load, test, and distribute Slay the Spire 2 mods. The first mod is **Random Character Options**, a multiplayer-compatible character-selection helper.

**[Download the Windows installer ZIP](https://github.com/rodneywells01/sts2-mods/releases/download/v0.1.2/RandomCharacterBlacklist-0.1.2.zip)**

**Players: [Start here — simple installation guide](INSTALL.md).**

**Coding agents: [Installation and development instructions](AGENTS.md).**

## Current state — September 6, 2026

The public installer above is **0.1.2**. This branch prepares **0.1.4** for **v0.111.0**, commit `41cef1ea`, with selectable reveal timing. Rodney reports that the prior version worked in a real Steam multiplayer match with an unmodded friend; that is player-reported validation of the prior version, not the new lock-in mode or a confirmed complete run.

For the local 0.1.4 review build, extract `dist/RandomCharacterBlacklist-0.1.4.zip`, close STS2, and run `Install.cmd`. Enable the mod in Settings → Mod Settings. Players need no SDK, BaseLib, or separate mod manager. See [validation](packaging/VALIDATION.md) and [implementation notes](docs/implementation.md). The [initial discovery](docs/modding-discovery.md) is historical context.

## First mod behavior

On standard character select, click the **dice icon** or press **F8**. Built by Rodney Wells (WatersEdge) with GPT-6 Astra.

- **Include in Random:** all characters start on; turn off unwanted characters.
- **Custom Random:** turn off to restore the game's normal seeded Random behavior.
- **Reveal immediately** (default): each Random activation rolls and shows the result, with one normal selection sound/screen shake even on repeats.
- **Reveal at lock-in**: Random keeps the game's mystery screen until you press **Embark / Ready**, then rolls from the currently included, visible, unlocked characters. You and the lobby can see the result once you lock in. This is a fresh local roll; Custom Random off remains the exact vanilla seed-based option.
- **Include all** resets roster choices. Icons and name colors come from the game.
- Individual character toggles play a brief native preview immediately left of the options dropdown: death when excluded, power when included. A new toggle immediately replaces the previous preview; Include all produces no animation.
- An **i** button provides hover help, a clickable explanation, and the credit “Built by Rodney Wells (WatersEdge) with GPT-6 Astra.” The dice header never takes mouse or arrow-key focus.
- Choices, enabled state, and reveal mode persist. Existing settings retain immediate reveal. Manual picks remain unrestricted.
- No eligible characters: immediate Random or mystery lock-in is blocked with an explanation.
- Normal lobby synchronization carries the ordinary selected character to peers.

The stable mod/package ID remains `RandomCharacterBlacklist` to preserve early-preview settings and avoid duplicates. Its saved exclusion set is the complement of the allow-by-default UI; newly available characters start included.

## Develop and distribute

Run `scripts/Build.ps1` (optionally with `-GamePath`) and `tests/Installer.Tests.ps1`. Build.ps1 uses .NET 9 or newer, builds Release, runs core checks, and creates the ZIP. Game assemblies, personal configuration, and the integration probe are excluded.

Steam Workshop is the existing automatic-update route; this package uses its native mod payload. No Workshop item has been published yet. The included installer supports sharing this preview directly with a friend. The next player check is the new lock-in mode in a Steam multiplayer session.
