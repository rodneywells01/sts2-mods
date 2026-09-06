RANDOM CHARACTER OPTIONS 0.1.1 - PREVIEW
For Slay the Spire 2 v0.111.0 on Windows

INSTALL
1. Close Slay the Spire 2.
2. Extract this entire ZIP to a folder. Do not run it inside the ZIP viewer.
3. Double-click Install.cmd. It finds Steam libraries and installs the two mod files.
4. Launch the game. Accept the game's mod-loading prompt and restart if requested.
5. Check Settings > Mod Settings for Random Character Options.

The installer is a readable PowerShell script, not a new mod manager. It downloads
nothing, checks the included file hashes, and does not edit your game saves.
No .NET SDK, BaseLib, or separate mod loader is needed to play.
An administrator prompt may be necessary if your Steam folder restricts writes.

MANUAL INSTALL (also suitable for mod managers that support local mod folders)
Copy the RandomCharacterBlacklist folder into:
  <Slay the Spire 2 installation>/mods/
Both the DLL and JSON must stay together. Do not copy Install.ps1 or checksums.json
into mods. Install one copy only; avoid duplicate local/Workshop copies.

USE
On standard character select, click the dice icon in the top-right, or press F8.
Under Include in Random, all characters start ON. Turn unwanted characters OFF.
Choices save automatically on this PC. Individual toggles show a brief character
animation on the left: death when OFF, power when ON. Another toggle immediately
replaces it. Include all does not start animations.
Click Random: it immediately reveals an allowed character and synchronizes the
ordinary character choice to your lobby. You can click Random again to reroll.
Unlike vanilla Random, it does not wait until embark and is not seed-deterministic.
Manual selection can still choose an excluded character. If all are excluded,
Random does nothing and asks you to allow at least one unlocked character.
Include all restores every character. Custom Random OFF restores native Random,
which resolves at embark. The i bubble provides help on hover or click. Preferences:

  %APPDATA%\SlayTheSpire2\RandomCharacterBlacklist\preferences.json
(The game may use a different user-data root on other installations.)

MULTIPLAYER
Each player has their own included-character list. A player's options never restrict teammates.
The mod submits a normal character selection before the run starts. It adds no
network messages and patches no run-generation or combat code. See VALIDATION.md
for the exact tested peer combinations and remaining checks for this preview.
Use matching game versions and compatible gameplay mods as usual.

REMOVE
Close the game, then run: Install.cmd -Uninstall
Or delete only the RandomCharacterBlacklist folder from the game's mods folder.
The installer keeps saved preferences and other mods. Disable in Mod Settings
if you only want to pause using it. The game separates modded and unmodded saves.

WORKSHOP
This package uses the game's native mod format. It is ready for later Workshop
packaging, but this preview has no published Workshop listing or auto-updater.

TROUBLESHOOTING
If blocked by game-version checking, request a matching build.
If the mod is listed but fails to work, check the game's logs/godot.log under its
user-data folder for RandomCharacterBlacklist messages. Report the game version.




