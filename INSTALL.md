# Install Random Character Options

**For Windows and Slay the Spire 2 v0.111.0.** You do not need coding tools, BaseLib, or another mod loader.

## Install — five steps

1. **Close Slay the Spire 2 completely.**
2. **[Download RandomCharacterBlacklist-0.1.2.zip](https://github.com/rodneywells01/sts2-mods/releases/download/v0.1.2/RandomCharacterBlacklist-0.1.2.zip).** Use the ZIP provided by Rodney or the matching asset under [GitHub Releases](https://github.com/rodneywells01/sts2-mods/releases). Do **not** download “Source code” or use the green Code button. The installer is listed under Assets on the release page.
3. **Right-click the downloaded ZIP → Extract All → Extract.** Open the extracted folder. You should see `Install.cmd`, `Install.ps1`, and a folder named `RandomCharacterBlacklist`. Do not run the installer from inside the ZIP.
4. **Double-click `Install.cmd`.** It finds your Steam installation. Wait for **“Installed Random Character Options 0.1.2”**. If it asks for the game folder, follow “Find the game folder” below. An error message means installation has not completed.
5. **Launch the game.** Enable mod loading if the game prompts you, and restart if requested. In **Settings → Mod Settings**, make sure **Random Character Options** is enabled.

**Check it worked:** open a standard character-selection screen. A **dice icon** should appear at the top right. Click it or press **F8**.

## Use it

- Under **Include in Random**, turn off characters you do not want. Keep at least one unlocked character on.
- Click the game's **Random** character button. Every click rolls again; the same character may appear twice.
- Turn **Custom Random** off to restore the game's normal Random behavior.
- Each player's choices apply only to that player. Install on each computer whose player wants these controls.

**Dice icon present, but no Random character button?** The game shows Random only when at least one player in the lobby has unlocked every character. This mod does not bypass unlocks.

## If installation did not work

| What happened | What to do |
| --- | --- |
| Installer asks for a folder | Use the game folder described below, not the ZIP folder or `mods` folder. |
| “Access denied” | Close the game, then right-click the extracted `Install.cmd` → **Run as administrator**. |
| Wrong game version | Get a mod build matching your game version. Do not bypass the installer's version check. |
| Package integrity check failed | Download and extract a fresh copy of the whole ZIP. Keep its files together. |
| Mod is missing from Mod Settings | Check the two-file layout below, then restart the game. |

**Find the game folder:** in Steam, right-click **Slay the Spire 2 → Manage → Browse local files**. The correct folder contains `SlayTheSpire2.exe`. Copy that folder's address if the installer asks for it.

**Manual installation fallback:** with the game closed, create a folder named `mods` inside that game folder if it does not exist. Copy the extracted **`RandomCharacterBlacklist` folder** into `mods`. The final layout must be:

```text
Slay the Spire 2/
  SlayTheSpire2.exe
  mods/
    RandomCharacterBlacklist/
      RandomCharacterBlacklist.dll
      RandomCharacterBlacklist.json
```

Do not put the whole ZIP/extracted package inside `mods`, and do not add a second nested `RandomCharacterBlacklist` folder. Then follow step 5.

## Update or remove

**Update:** close the game, extract the new complete ZIP, and run its `Install.cmd`. Your saved choices are kept.

**Remove:** close the game and delete only `mods/RandomCharacterBlacklist` from the game folder. Saved preferences remain. To pause the custom random behavior instead, turn **Custom Random** off in the dice menu.

There is no published Workshop listing yet. If you later install through Workshop, remove the manual copy first so the game loads only one copy.

The game separates modded and unmodded saves. This installer does not move or convert saves. This preview has passed local multiplayer tests; a real Steam friend session and full-run playtest remain outstanding. See `VALIDATION.md` in the ZIP or [validation details](packaging/VALIDATION.md) in the repository.
