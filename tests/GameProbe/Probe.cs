using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using System.Text.Json;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    private static string role = System.Environment.GetEnvironmentVariable("RCB_PROBE_ROLE") ?? "host";
    private static string output = System.Environment.GetEnvironmentVariable("RCB_PROBE_OUTPUT") ?? throw new Exception("Probe requires output path");
    private static bool started;
    private static bool lockIn = System.Environment.GetEnvironmentVariable("RCB_PROBE_MODE") == "lock-in";
    private static bool nativePeerRandom = System.Environment.GetEnvironmentVariable("RCB_PROBE_NATIVE_PEER") == "1";
    private static int selectionSounds;
    private static int selectionShakes;
    private static string? expectedSound;
    private static readonly List<string> checks = new();
    public static void Initialize()
    {
        var harmony = new Harmony("rcb.integration.probe");
        harmony.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.SfxCmd), "Play", new[] { typeof(string), typeof(float) }), prefix: new HarmonyMethod(typeof(Probe), nameof(Sound)));
        harmony.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "ScreenShake"), prefix: new HarmonyMethod(typeof(Probe), nameof(Shake)));
        harmony.Patch(AccessTools.Method(typeof(NCharacterSelectScreen), "AfterInitialized"), postfix: new HarmonyMethod(typeof(Probe), nameof(Ready)));
        harmony.Patch(AccessTools.Method(typeof(StartRunLobby), "BeginRunLocally"), postfix: new HarmonyMethod(typeof(Probe), nameof(Began)));
        // Synthetic UI profiles have no progression. Reapply their test roster
        // whenever the game refreshes visibility, including lobby re-entry/joins.
        if (role is "ui" or "layout")
            harmony.Patch(AccessTools.Method(typeof(NCharacterSelectScreen), "UpdateRandomCharacterVisibility"), postfix: new HarmonyMethod(typeof(Probe), nameof(PreviewRoster)));
        GD.Print("[RCBProbe] Loaded " + role);
    }
    private static void Record(string check)
    {
        checks.Add(check);
        File.WriteAllText(output, JsonSerializer.Serialize(new { role, checks }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("[RCBProbe] " + check);
    }
    private static void Assert(bool condition, string text) { if (!condition) throw new Exception(text); Record("PASS " + text); }
    private static void Sound(string sfx) { if (expectedSound != null && sfx == expectedSound) selectionSounds++; }
    private static void Shake() { if (expectedSound != null) selectionShakes++; }
    private static void PreviewRoster(NCharacterSelectScreen __instance)
    {
        foreach (var button in __instance.GetNode<Control>("CharSelectButtons/ButtonContainer").GetChildren().OfType<NCharacterSelectButton>())
        {
            button.DebugUnlock();
            button.Visible = true;
        }
        GD.Print("[RCBProbe] UI preview roster restored after visibility refresh");
    }
    private static void Ready(NCharacterSelectScreen __instance) { if (!started) { started = true; _ = Run(__instance); } }
    private static async Task Wait(Node node, double seconds) => await node.ToSignal(node.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static async Task Run(NCharacterSelectScreen screen)
    {
        try
        {
            await Wait(screen, 2);
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree()) return;
            string isolatedRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!, "appdata"));
            string userData = Path.GetFullPath(OS.GetUserDataDir());
            if (!userData.StartsWith(isolatedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Probe requires isolated user data under output sibling appdata: " + userData);
            Record("Isolated user data: " + userData);
            MegaCrit.Sts2.Core.Saves.SaveManager.Instance.MarkFtueAsComplete("accept_tutorials_ftue");
            if (role == "singleplayer")
            {
                screen.Lobby.CleanUp(disconnectSession: true);
                screen.InitializeSingleplayer();
            }
            var buttons = screen.GetNode<Control>("CharSelectButtons/ButtonContainer").GetChildren().OfType<NCharacterSelectButton>().ToList();
            foreach (var button in buttons) { button.DebugUnlock(); button.Visible = true; }
            if (role is "ui" or "layout")
            {
                for (int i = 0; i < 3; i++)
                {
                    AccessTools.Method(typeof(NCharacterSelectScreen), "UpdateRandomCharacterVisibility").Invoke(screen, null);
                    Assert(buttons.Single(b => b.IsRandom).Visible, "UI Random survives native visibility refresh " + i);
                }
                if (role == "layout") await CheckLayout(screen);
                else Record("UI_READY");
                return;
            }
            if (role == "animation")
            {
                var animationPanel = screen.GetNode<Control>("RandomCharacterBlacklistPanel");
                var options = Descendants(animationPanel).OfType<CheckButton>().Where(b => b.Text != "Custom Random").ToArray();
                var original = screen.Lobby.LocalPlayer.character;
                foreach (var option in options)
                {
                    foreach (bool included in new[] { false, true })
                    {
                        option.SetPressedNoSignal(!included);
                        option.ButtonPressed = included;
                        await Wait(screen, 0.2);
                        var shown = screen.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals>().Where(n => n.Visible).ToArray();
                        Assert(shown.Length == 1, option.Text + " has one visible preview");
                        string? clip = shown[0].SpineBody?.TryGetAnimationState()?.GetCurrentAnimationName();
                        Assert(clip == (included ? (shown[0].Name.ToString().Contains("NECROBINDER") ? "cast_mighty" : shown[0].Name.ToString().Contains("DEFECT") ? "process" : "cast") : "die"), option.Text + " plays " + clip);
                    }
                }
                for (int i = 0; i < 30; i++)
                {
                    var option = options[i % options.Length];
                    option.ButtonPressed = !option.ButtonPressed;
                    var shown = screen.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals>().Where(n => n.Visible).ToArray();
                    Assert(shown.Length == 1, "Rapid toggle replaces previous preview " + i);
                }
                await Wait(screen, 5.5);
                Assert(!screen.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals>().Any(n => n.Visible), "Preview fades and hides");
                Descendants(animationPanel).OfType<Button>().Single(b => b.Text == "Include all").EmitSignal(BaseButton.SignalName.Pressed);
                Assert(!screen.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals>().Any(n => n.Visible), "Include all creates no preview");
                Assert(screen.Lobby.LocalPlayer.character == original, "Animations leave lobby character unchanged");
                Record("COMPLETE"); return;
            }
            var random = buttons.Single(b => b.IsRandom);
            var desired = buttons.First(b => b.Character.Id.Entry == (role == "client" ? "SILENT" : "IRONCLAD"));
            var panel = screen.GetNodeOrNull<Control>("RandomCharacterBlacklistPanel");
            bool modded = panel != null;
            Record("Modded=" + modded);
            if (modded)
            {
                var mode = Descendants(panel!).OfType<CheckButton>().Single(b => b.Text == "Custom Random");
                mode.ButtonPressed = true;
                var immediate = Descendants(panel!).OfType<Button>().Single(b => b.Name == "RevealImmediately");
                var delayed = Descendants(panel!).OfType<Button>().Single(b => b.Name == "RevealAtLockIn");
                immediate.EmitSignal(BaseButton.SignalName.Pressed);
                var options = Descendants(panel!).OfType<CheckButton>().Where(b => b != mode).ToList();
                Assert(options.Count == buttons.Count - 1, "Panel lists roster");
                foreach (var option in options) option.ButtonPressed = false;
                var previous = screen.Lobby.LocalPlayer.character.Id;
                random.ForceClick();
                Assert(screen.Lobby.LocalPlayer.character.Id == previous, "All excluded preserves lobby choice");
                var allowed = options.Single(o => o.Text.StartsWith(desired.Character.Title.GetFormattedText()));
                allowed.ButtonPressed = true;
                // First roll changes character; nineteen repeats must also emit feedback.
                buttons.First(b => !b.IsRandom && b != desired).Select();
                expectedSound = desired.Character.CharacterSelectSfx;
                selectionSounds = selectionShakes = 0;
                for (int i = 0; i < 20; i++)
                {
                    random.ForceClick();
                    if (screen.Lobby.LocalPlayer.character != desired.Character) throw new Exception("Forbidden random character");
                }
                Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Twenty random picks honor single eligible character");
                expectedSound = null;
                Assert(selectionSounds == 20 && selectionShakes == 20, "Changed and repeated rolls each emit exactly one character sound and screen shake");
                mode.ButtonPressed = false;
                random.Select();
                Assert(screen.Lobby.LocalPlayer.character is MegaCrit.Sts2.Core.Models.Characters.RandomCharacter, "Disabled mode restores vanilla Random placeholder");
                random.ForceClick();
                Assert(screen.Lobby.LocalPlayer.character is MegaCrit.Sts2.Core.Models.Characters.RandomCharacter, "Disabled activation does not roll custom choice");
                mode.ButtonPressed = true;
                Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Enabling resolves pending vanilla Random through whitelist");
                var excluded = buttons.First(b => !b.IsRandom && b != desired);
                excluded.Select();
                Assert(screen.Lobby.LocalPlayer.character == excluded.Character, "Manual selection ignores blacklist");
                random.ForceClick();
                if (lockIn)
                {
                    delayed.EmitSignal(BaseButton.SignalName.Pressed);
                    Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Changing reveal mode preserves concrete selection");
                    random.Select();
                    for (int i = 0; i < 20; i++) random.ForceClick();
                    Assert(screen.Lobby.LocalPlayer.character is MegaCrit.Sts2.Core.Models.Characters.RandomCharacter,
                        "Lock-in mode keeps native mystery through twenty Random activations");
                    allowed.ButtonPressed = false;
                    var embark = (MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton)AccessTools.Field(typeof(NCharacterSelectScreen), "_embarkButton").GetValue(screen)!;
                    embark.Disable(); // Match the state on the tutorial confirmation callback.
                    AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed").Invoke(screen, new object?[] { null });
                    Assert(!screen.Lobby.LocalPlayer.isReady && screen.Lobby.LocalPlayer.character is MegaCrit.Sts2.Core.Models.Characters.RandomCharacter,
                        "Empty pool blocks Embark and preserves mystery without marking ready");
                    Assert(embark.IsEnabled, "Failed lock-in restores Embark after tutorial callback state");
                    allowed.ButtonPressed = true;
                    immediate.EmitSignal(BaseButton.SignalName.Pressed);
                    Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Switching pending mystery to immediate resolves allowed choice");
                    delayed.EmitSignal(BaseButton.SignalName.Pressed);
                    excluded.Select();
                    Assert(screen.Lobby.LocalPlayer.character == excluded.Character, "Lock-in mode leaves manual excluded picks unrestricted");
                    random.Select();
                    // Opening from focused Random must not restore that focus on
                    // Embark and accidentally replace the just-resolved choice.
                    var dice = Descendants(panel!).OfType<Button>().Single(b => b.Name == "RandomOptionsDice");
                    var dropdown = Descendants(panel!).OfType<Control>().Single(b => b.Name == "RandomOptionsDropdown");
                    if (dropdown.Visible) dice.EmitSignal(BaseButton.SignalName.Pressed);
                    random.GrabFocus();
                    dice.EmitSignal(BaseButton.SignalName.Pressed);
                    Assert(dropdown.Visible, "Lock-in test leaves options open from Random focus");
                }
            }
            else if (nativePeerRandom) random.Select();
            else desired.Select();
            Record("Local choice " + screen.Lobby.LocalPlayer.character.Id);
            if (role == "singleplayer")
            {
                Assert(screen.Lobby.LocalPlayer.character == random.Character, "Singleplayer mystery stays pending before Embark");
                AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed").Invoke(screen, new object?[] { null });
                return;
            }
            for (int i = 0; i < 60 && screen.Lobby.Players.Count < 2; i++) await Wait(screen, 1);
            Assert(screen.Lobby.Players.Count == 2, "Two peers connected");
            await Wait(screen, 3);
            if (!lockIn)
                Assert(screen.Lobby.Players.Any(p => p.character.Id.Entry == "IRONCLAD") && screen.Lobby.Players.Any(p => p.character.Id.Entry == "SILENT"), "Both peers agree on Ironclad and Silent");
            else if (!nativePeerRandom)
                Assert(screen.Lobby.LocalPlayer.character == (modded ? random.Character : desired.Character), "Mystery remains pending until local lock-in");
            AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed").Invoke(screen, new object?[] { null });
            if (modded || !nativePeerRandom)
                Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Embark locks in allowed local character");
            Record("Ready to start");
        }
        catch (Exception error) { Record("FAIL " + error); }
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private static async Task CheckLayout(NCharacterSelectScreen screen)
    {
        var panel = screen.GetNode<Control>("RandomCharacterBlacklistPanel");
        var dice = Descendants(panel).OfType<Button>().Single(b => b.Name == "RandomOptionsDice");
        Assert(dice.Icon.GetSize() == new Vector2(232, 160), "Dice raster supplies four times the logical resolution");
        Assert(dice.FocusMode == Control.FocusModeEnum.None, "Dice does not enter native focus navigation");
        Assert(panel.Theme.DefaultFont is FontFile { MultichannelSignedDistanceField: true } or SystemFont { MultichannelSignedDistanceField: true },
            "Panel owns a scalable MSDF font");
        dice.EmitSignal(BaseButton.SignalName.Pressed);
        var dropdown = Descendants(panel).OfType<Control>().Single(b => b.Name == "RandomOptionsDropdown");
        var delayed = Descendants(panel).OfType<Button>().Single(b => b.Name == "RevealAtLockIn");
        delayed.GrabFocus();
        screen._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Assert(delayed.ButtonPressed && !screen.Lobby.LocalPlayer.isReady, "Keyboard Enter selects lock-in mode without readying the player");
        Descendants(panel).OfType<Button>().Single(b => b.Text == "Include all").EmitSignal(BaseButton.SignalName.Pressed);
        Assert(delayed.ButtonPressed, "Include all preserves reveal mode");
        Assert(Descendants(panel).OfType<CheckButton>().All(b => b.ButtonPressed), "Include all restores the roster");
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(2560, 1440), new Vector2I(3840, 2160) })
        {
            DisplayServer.WindowSetSize(size);
            await Wait(screen, 1);
            var rect = panel.GetGlobalRect();
            var viewport = screen.GetGlobalRect();
            Assert(viewport.Encloses(rect), "Options stay within screen at " + size);
            Assert(dropdown.Visible, "Options remain open after resize " + size);
            await screen.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var captured = screen.GetViewport().GetTexture().GetImage();
            captured.SavePng(Path.Combine(Path.GetDirectoryName(output)!, $"reveal-{size.X}x{size.Y}.png"));
            Record("Captured image " + captured.GetSize());
        }
        DisplayServer.WindowSetSize(new Vector2I(1280, 720));
        Record("COMPLETE");
    }
    private static void Began(StartRunLobby __instance)
    {
        try
        {
            if (role == "singleplayer")
                Assert(__instance.Players.Count == 1 && __instance.LocalPlayer.character.Id.Entry == "IRONCLAD", "Singleplayer lock-in honors included pool through run initialization");
            else if (!nativePeerRandom)
                Assert(__instance.Players.Any(p => p.character.Id.Entry == "IRONCLAD") && __instance.Players.Any(p => p.character.Id.Entry == "SILENT"), "Run initialization retains agreed characters");
            else
                Assert(__instance.Players.All(p => p.character is not MegaCrit.Sts2.Core.Models.Characters.RandomCharacter), "Native teammate Random resolves at run start");
            Record("Run roster " + string.Join(",", __instance.Players.OrderBy(p => p.id).Select(p => p.id + ":" + p.character.Id)));
            Record("COMPLETE");
        }
        catch (Exception error) { Record("FAIL " + error); }
    }
}

