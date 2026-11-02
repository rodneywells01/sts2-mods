# Relic Rock Paper Scissors Stats

A separate, optional mod for Windows **Slay the Spire 2 v0.111.0**, version **0.1.0**. It records the native multiplayer treasure-relic rock-paper-scissors results on your computer. Teammates do not need the mod to play with you; install it on each computer that wants its own history. It does not change votes, rolls, RNG, relic awards, network messages or saves.

## Install the local preview

1. Build with `scripts/Build-Rps.ps1`, or use the complete `RelicRpsStats-0.1.0.zip` supplied by Rodney. No public release is implied by a local build.
2. Close the game. In Steam, use **Manage → Browse local files** and confirm `SlayTheSpire2.exe` and `release_info.json` version `v0.111.0`.
3. Extract the ZIP. Copy its **RelicRpsStats** folder into the game's `mods` folder. The final files are `mods/RelicRpsStats/RelicRpsStats.dll` and `mods/RelicRpsStats/RelicRpsStats.json`. Do not copy the entire package into `mods`.
4. Launch the game and enable **Relic Rock Paper Scissors Stats** in **Settings → Mod Settings**, restarting if requested.

Players need no SDK, BaseLib or test probe. Random Character Options is independent. Remove only `mods/RelicRpsStats` to uninstall; your stats remain.

## View your record

- In multiplayer treasure selection, a panel shows your lifetime contested-relic record and individual throw totals. Click **Rock Paper Scissors stats** for teammate records.
- From the main menu, open **Compendium → Rock Paper Scissors stats** to view saved records without starting a run.
- The viewer has local player/account selection, global teammate search, and 25 teammates per page. Names are plain text; identities use platform and player ID so renamed friends retain their records.
- A contest is recorded only when you participated in rock-paper-scissors for that relic. Uncontested choices, skips, empty chests, consolation awards, singleplayer, fake multiplayer and replays do not count.
- Relic win rate = contested relics won / contested relics played. Throw win rate = winning throws / decisive throws; ties are listed separately. In three/four-player rounds, three distinct moves tie the whole round; eliminated players stop throwing.
- Teammate relic rows show **W** when you won, **L** when that teammate won, and **Other** when another participant won. Teammate throw rows compare your moves while both remain active; matching moves tie, even if both beat another participant. A three-move round ties for everyone. Lifetime throw totals describe your outcome in the entire native round, so they need not equal the sum of teammate rows.
- Native results are computed in advance. The displayed summary refreshes after the animation; the detailed viewer is unavailable while the animation runs, to avoid revealing the winner early.

## Storage and recovery

History begins when installed; existing run history cannot reconstruct past throws. The file is `<game user-data directory>/RelicRpsStats/stats.json`, outside the game's mod-manifest scan. It separates each local account and platform. It is local only, with no upload or teammate synchronization. It contains platform/player IDs, cached display names, a hashed contest identity and moves; back it up if moving computers.

The identity includes the run start timestamp, seed, roster, room location and relic slot/ID. Reopening/reloading the same contest does not count again, even if the native result is rolled differently. A newly started seeded run has a different start timestamp.

Saves run in the background, replace the file atomically, and retain the prior version as `stats.json.bak`. A failed save keeps session totals and retries the full history on the next result; exiting before a pending save finishes can lose unsaved results. An invalid/unsupported file pauses recording and is preserved. Close the game, back up the file and its `.bak`, then restore a known-good copy before restarting. Files over 64 MiB are refused without overwriting the prior file; no automatic pruning removes your history.

## Develop and verify

Use .NET 9 and installed game assemblies. `scripts/Build-Rps.ps1 -GamePath <path>` validates the game version, builds, runs game-independent tests, and produces a two-file mod payload ZIP. It never installs into the player's game or publishes a release.

`tests/RpsCoreTests` covers move pairs, ties, progressive elimination, account/opponent identity, malformed history, duplicates across reload, atomic replacement, write failures and ordered retry. `tests/RpsProbe` is a development dependency only. After preparing the isolated copies described in `docs/implementation.md`, build the probe and use `scripts/Test-Rps.ps1 -Case both`, `host-only`, or `client-only`. Every probe asserts its isolated `STS2ModTests` user-data path. Mixed tests include the read-only probe on both peers, with the stats initializer absent from the unmodded peer. `-Case compendium -Visible` exercises the actual native viewer with 10,000 synthetic teammate records, global search and repeated paging. Synthetic records never enter a player's real history.

Local instrumented ENet tests do not establish an uninstrumented Steam friend session, physical-controller support, other mods, future game versions, or a full combat run. See the PR's Validation for the tested revision, observed results and visual evidence.

Built by Rodney Wells (WatersEdge) with Codex.
