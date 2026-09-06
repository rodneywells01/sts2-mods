# Slay the Spire 2 Mods

Create, load, test, and distribute Slay the Spire 2 mods. The first mod is **Random Character Options**, a multiplayer-compatible character-selection helper.

## Current state — September 5, 2026

Playable Windows preview built for **v0.111.0**, commit `41cef1ea`. Core and installer checks pass. Two local game processes tested both-installed, host-only, and client-only configurations with consistent lobby choices and run initialization. These instrumented local tests do not replace a real Steam friend session or full-run/combat playthrough.

Extract `dist/RandomCharacterBlacklist-0.1.1.zip`, close STS2, and run `Install.cmd`. Enable the mod in Settings → Mod Settings. Players need no SDK, BaseLib, or separate mod manager. See [validation](packaging/VALIDATION.md) and [implementation notes](docs/implementation.md). The [initial discovery](docs/modding-discovery.md) is historical context.

## First mod behavior

On standard character select, click the **dice icon** or press **F8**. Built by Rodney Wells (WatersEdge) with GPT-6 Astra.

- **Include in Random:** all characters start on; turn off unwanted characters.
- **Custom Random:** turn off to restore the game's normal seeded Random behavior.
- Each click/activation of Random rolls again and immediately reveals the result. The same character can legitimately appear twice; the result pulses on every roll.
- **Include all** resets roster choices. Icons and name colors come from the game.
- Individual character toggles play a brief native preview immediately left of the options dropdown: death when excluded, power when included. A new toggle immediately replaces the previous preview; Include all produces no animation.
- An **i** button provides hover help, a clickable explanation, and the credit “Built by Rodney Wells (WatersEdge) with GPT-6 Astra.” The dice header never takes mouse or arrow-key focus.
- Choices and enabled state persist. Manual picks remain unrestricted.
- No eligible characters: Random does nothing and explains the problem.
- Normal lobby synchronization carries the ordinary selected character to peers.

The stable mod/package ID remains `RandomCharacterBlacklist` to preserve early-preview settings and avoid duplicates. Its saved exclusion set is the complement of the allow-by-default UI; newly available characters start included.

## Develop and distribute

Run `scripts/Build.ps1` (optionally with `-GamePath`) and `tests/Installer.Tests.ps1`. Build.ps1 uses .NET 9 or newer, builds Release, runs core checks, and creates the ZIP. Game assemblies, personal configuration, and the integration probe are excluded.

Steam Workshop is the existing automatic-update route; this package uses its native mod payload. No Workshop item has been published yet. The included installer supports sharing this preview directly with a friend. The next playtest is a real Steam multiplayer session.






