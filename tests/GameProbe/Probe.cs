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
    private static readonly List<string> checks = new();
    public static void Initialize()
    {
        var harmony = new Harmony("rcb.integration.probe");
        harmony.Patch(AccessTools.Method(typeof(NCharacterSelectScreen), "AfterInitialized"), postfix: new HarmonyMethod(typeof(Probe), nameof(Ready)));
        harmony.Patch(AccessTools.Method(typeof(StartRunLobby), "BeginRunLocally"), postfix: new HarmonyMethod(typeof(Probe), nameof(Began)));
        // Synthetic UI profiles have no progression. Reapply their test roster
        // whenever the game refreshes visibility, including lobby re-entry/joins.
        if (role == "ui")
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
            var buttons = screen.GetNode<Control>("CharSelectButtons/ButtonContainer").GetChildren().OfType<NCharacterSelectButton>().ToList();
            foreach (var button in buttons) { button.DebugUnlock(); button.Visible = true; }
            if (role == "ui")
            {
                for (int i = 0; i < 3; i++)
                {
                    AccessTools.Method(typeof(NCharacterSelectScreen), "UpdateRandomCharacterVisibility").Invoke(screen, null);
                    Assert(buttons.Single(b => b.IsRandom).Visible, "UI Random survives native visibility refresh " + i);
                }
                Record("UI_READY"); return;
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
                var options = Descendants(panel!).OfType<CheckButton>().Where(b => b != mode).ToList();
                Assert(options.Count == buttons.Count - 1, "Panel lists roster");
                foreach (var option in options) option.ButtonPressed = false;
                var previous = screen.Lobby.LocalPlayer.character.Id;
                random.ForceClick();
                Assert(screen.Lobby.LocalPlayer.character.Id == previous, "All excluded preserves lobby choice");
                var allowed = options.Single(o => o.Text.StartsWith(desired.Character.Title.GetFormattedText()));
                allowed.ButtonPressed = true;
                for (int i = 0; i < 20; i++)
                {
                    random.ForceClick();
                    if (screen.Lobby.LocalPlayer.character != desired.Character) throw new Exception("Forbidden random character");
                }
                Assert(screen.Lobby.LocalPlayer.character == desired.Character, "Twenty random picks honor single eligible character");
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
            }
            else desired.Select();
            Record("Local choice " + screen.Lobby.LocalPlayer.character.Id);
            for (int i = 0; i < 60 && screen.Lobby.Players.Count < 2; i++) await Wait(screen, 1);
            Assert(screen.Lobby.Players.Count == 2, "Two peers connected");
            await Wait(screen, 3);
            Assert(screen.Lobby.Players.Any(p => p.character.Id.Entry == "IRONCLAD") && screen.Lobby.Players.Any(p => p.character.Id.Entry == "SILENT"), "Both peers agree on Ironclad and Silent");
            screen.Lobby.SetReady(true);
            Record("Ready to start");
        }
        catch (Exception error) { Record("FAIL " + error); }
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Began(StartRunLobby __instance)
    {
        try
        {
            Assert(__instance.Players.Any(p => p.character.Id.Entry == "IRONCLAD") && __instance.Players.Any(p => p.character.Id.Entry == "SILENT"), "Run initialization retains agreed characters");
            Record("COMPLETE");
        }
        catch (Exception error) { Record("FAIL " + error); }
    }
}

