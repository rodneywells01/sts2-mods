using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace RandomCharacterBlacklist;

[ModInitializer(nameof(Initialize))]
public static class Mod
{
    public const string Id = "RandomCharacterBlacklist";
    private static readonly ConditionalWeakTable<NCharacterSelectScreen, BlacklistPanel> Panels = new();
    internal static Preferences Preferences = new();
    internal static string? PreferenceError;
    internal static string PreferencePath = "";

    public static void Initialize()
    {
        PreferencePath = Path.Combine(OS.GetUserDataDir(), Id, "preferences.json");
        try { Preferences = Preferences.Load(PreferencePath); }
        catch (Exception error)
        {
            // Preserve the invalid file; do not silently discard a previously saved blacklist.
            PreferenceError = "Saved choices could not be read. Open Random options and reset to continue.";
            GD.PrintErr($"[{Id}] Preferences: {error.Message}");
        }
        var harmony = new Harmony("rodneywells.randomcharacterblacklist");
        Patch(harmony, typeof(NCharacterSelectScreen), "_Ready", nameof(ScreenReady), false);
        Patch(harmony, typeof(NCharacterSelectScreen), "_Input", nameof(ScreenInput), true);
        Patch(harmony, typeof(NCharacterSelectButton), "Select", nameof(SelectPrefix), true);
        GD.Print($"[{Id}] 0.1.1 loaded; local choice patches installed. Preferences: {PreferencePath}");
    }

    private static void Patch(Harmony harmony, Type type, string target, string patch, bool prefix)
    {
        var method = AccessTools.DeclaredMethod(type, target) ?? throw new MissingMethodException(type.FullName, target);
        var hook = new HarmonyMethod(typeof(Mod).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic)!);
        harmony.Patch(method, prefix: prefix ? hook : null, postfix: prefix ? null : hook);
    }

    private static void ScreenReady(NCharacterSelectScreen __instance)
    {
        try { Panels.GetValue(__instance, screen => new BlacklistPanel(screen)); }
        catch (Exception error) { GD.PrintErr($"[{Id}] Panel creation failed: {error}"); }
    }

    private static bool ScreenInput(NCharacterSelectScreen __instance, InputEvent inputEvent)
    {
        if (!__instance.IsVisibleInTree()) return true;
        if (Panels.TryGetValue(__instance, out var focusedPanel) && focusedPanel.HandleConfirm(inputEvent))
        {
            __instance.GetViewport().SetInputAsHandled();
            return false;
        }
        if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 } && Panels.TryGetValue(__instance, out var panel))
        {
            panel.Toggle();
            __instance.GetViewport().SetInputAsHandled();
            return false;
        }
        if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } &&
            Panels.TryGetValue(__instance, out var openPanel) && openPanel.IsOpen)
        {
            openPanel.Toggle();
            __instance.GetViewport().SetInputAsHandled();
            return false;
        }
        return true;
    }

    private static bool SelectPrefix(NCharacterSelectButton __instance, ICharacterSelectButtonDelegate ____delegate)
    {
        if (!__instance.IsRandom || ____delegate is not NCharacterSelectScreen screen) return true;
        // Vanilla selects on FocusEntered, which doesn't fire on repeated clicks.
        // While enabled, suppress that placeholder and roll on the Released signal instead.
        // Disabling the feature lets the original method and seeded resolution run untouched.
        return !Preferences.Enabled;
    }

    internal static void Roll(NCharacterSelectScreen screen)
    {
        if (!Preferences.Enabled) return;
        try
        {
            var panel = Panels.GetValue(screen, value => new BlacklistPanel(value));
            if (PreferenceError != null) { panel.ShowMessage(PreferenceError); return; }
            var pool = SelectionPool.Eligible(panel.CharacterButtons(), b => b.Character.Id.ToString(),
                b => !b.IsRandom && !b.IsLocked && b.Visible, Preferences.ExcludedCharacters);
            if (!SelectionPool.TryPick(pool, System.Random.Shared.Next, out var chosen))
            {
                panel.ShowMessage("Include at least one unlocked character to use Random.");
                return;
            }
            // Select() is intentionally a no-op when the chosen character is already selected.
            // The lobby already has that exact character, so this is a valid random outcome.
            chosen!.Select();
            panel.ShowResult(chosen.Character.Title.GetFormattedText());
            GD.Print($"[{Id}] Random chose {chosen.Character.Id} from {pool.Count} eligible characters.");
        }
        catch (Exception error)
        {
            GD.PrintErr($"[{Id}] Random selection stopped: {error}");
            if (Panels.TryGetValue(screen, out var panel)) panel.ShowMessage("Random could not select a character. See the game log.");
        }
    }
}

