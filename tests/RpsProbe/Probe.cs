using System.Diagnostics;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RelicRpsStats;
using Mod = RelicRpsStats.Mod;
using Environment = System.Environment;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    private static readonly string role = Environment.GetEnvironmentVariable("RPS_PROBE_ROLE") ?? "host";
    private static readonly string output = Environment.GetEnvironmentVariable("RPS_PROBE_OUTPUT") ?? throw new Exception("Output required");
    private static readonly List<string> checks = new();
    private static bool started;
    private static StatsBook Book => (StatsBook)AccessTools.Field(typeof(Mod), "Book").GetValue(null)!;
    private static Task Pending => (Task)AccessTools.Field(typeof(Mod), "PendingSave").GetValue(null)!;
    public static void Initialize()
    {
        if (!OS.GetUserDataDir().Replace('\\', '/').Contains("STS2ModTests/")) throw new Exception("Refusing non-isolated user data");
        // The game's loader resolves only enabled mod assemblies. Mixed-peer observation needs the
        // DTO assembly explicitly, without running its initializer or installing any of its patches.
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "RelicRpsStats"))
            System.Reflection.Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(typeof(Probe).Assembly.Location)!, "RelicRpsStats.dll"));
        var h = new Harmony("rps.isolated.probe");
        if (role == "compendium")
            h.Patch(AccessTools.Method(typeof(NMainMenu), "_Ready"), postfix: new HarmonyMethod(typeof(Probe), nameof(MainReady)));
        else
        {
            h.Patch(AccessTools.Method(typeof(NCharacterSelectScreen), "AfterInitialized"), postfix: new HarmonyMethod(typeof(Probe), nameof(LobbyReady)));
            h.Patch(AccessTools.Method(typeof(RunManager), "Launch"), postfix: new HarmonyMethod(typeof(Probe), nameof(RunReady)));
        }
        Record("Isolated data: " + OS.GetUserDataDir());
    }
    private static void Record(string check)
    {
        checks.Add(check); File.WriteAllText(output, JsonSerializer.Serialize(new { role, checks }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("[RpsProbe] " + check);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Record("PASS " + message); }
    private static async Task Wait(double seconds) => await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static IEnumerable<Node> Nodes(Node node)
    { foreach (var child in node.GetChildren()) { yield return child; foreach (var nested in Nodes(child)) yield return nested; } }
    private static void LobbyReady(NCharacterSelectScreen __instance) { if (!started) { started = true; _ = Lobby(__instance); } }
    private static async Task Lobby(NCharacterSelectScreen screen)
    {
        try
        {
            await Wait(2);
            var buttons = Nodes(screen).OfType<NCharacterSelectButton>().Where(b => !b.IsRandom).ToArray();
            foreach (var b in buttons) b.DebugUnlock();
            buttons[role == "host" ? 0 : 1].Select();
            for (int i = 0; i < 40 && screen.Lobby.Players.Count < 2; i++) await Wait(1);
            Check(screen.Lobby.Players.Count == 2, "Two peers connected");
            await Wait(2); screen.Lobby.SetReady(true);
        }
        catch (Exception e) { Record("FAIL " + e); }
    }
    private static void RunReady() => _ = Treasure();
    private static async Task Treasure()
    {
        try
        {
            await Wait(6);
            await RunManager.Instance.EnterRoomDebug(RoomType.Treasure, showTransition: false);
            await Wait(2);
            var room = Nodes(NGame.Instance!).OfType<NTreasureRoom>().Single();
            var sync = RunManager.Instance.TreasureRoomRelicSynchronizer;
            List<RelicPickingResult>? results = null;
            sync.RelicsAwarded += r => results = r;
            _ = (Task)AccessTools.Method(typeof(NTreasureRoom), "OpenChest").Invoke(room, null)!;
            for (int i = 0; i < 40 && sync.CurrentRelics == null; i++) await Wait(0.2);
            Check(sync.CurrentRelics?.Count >= 2, "Native multiplayer relic choices");
            var screen = Nodes(room).OfType<NTreasureRoomRelicCollection>().Single();
            bool installed = Nodes(room).Any(n => n.Name == "RelicRpsSummary");
            Record("Modded=" + installed);
            await Wait(2);
            if (installed && role == "host")
            {
                Nodes(room).OfType<Button>().Single(b => b.Text == "Rock Paper Scissors stats").EmitSignal(BaseButton.SignalName.Pressed);
                Check(Nodes(room).OfType<AcceptDialog>().Any(d => d.Visible), "Stats can open while waiting for peer vote");
            }
            sync.PickRelicLocally(0);
            for (int i = 0; i < 100 && results == null; i++) await Wait(0.2);
            Check(results != null, "Native synchronized award event");
            if (installed) Check(!Nodes(room).OfType<AcceptDialog>().Any(d => d.Visible), "Active stats viewer closes before precomputed result is recorded");
            var fight = results!.Single(r => r.type == RelicPickingResultType.FoughtOver);
            Record("Winner=" + fight.player!.NetId + ";rounds=" + JsonSerializer.Serialize(fight.fight!.rounds.Select(r => r.moves)));
            await screen.RelicPickingFinished();
            await Wait(0.6);
            if (installed)
            {
                await Pending;
                Check(Book.Events.Count == 1, "One contested relic saved; consolation excluded");
                var c = Book.Events.Single();
                Check(c.Winner.EndsWith(":" + fight.player.NetId) && c.Rounds.Length == fight.fight.rounds.Count, "Saved native winner and every throw");
                Check(!Book.Add(c), "Duplicate delivery ignored");
                var store = new StatsStore(Path.Combine(OS.GetUserDataDir(), Mod.Id, "stats.json"));
                Check(store.Load().Events.Count == 1, "Persisted record reloads");
                var summary = Nodes(room).OfType<Control>().Single(n => n.Name == "RelicRpsSummary");
                Check(summary.IsVisibleInTree(), "Updated stats remain visible after native award screen closes");
                // Keep the actual selection interface visible for review after the native animation.
                screen.Visible = true; screen.Modulate = Colors.White;
                await Wait(0.4);
                if (DisplayServer.GetName() != "headless")
                    NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(output + ".png");
            }
            Record("COMPLETE");
        }
        catch (Exception e) { Record("FAIL " + e); }
    }
    private static void MainReady(NMainMenu __instance) { if (!started) { started = true; _ = Compendium(__instance); } }
    private static async Task Compendium(NMainMenu main)
    {
        try
        {
            await Wait(3);
            // Intentional upper-bound fixture: 10,000 teammates with long, untrusted display names.
            for (int i = 0; i < 10000; i++)
                Book.Add(new Contest(StatsBook.Key("fixture" + i), "None:1", ["None:1", "None:" + (i + 2)],
                    ["Fixture Player", $"Teammate {i:D5} [b]" + new string('W', 80)], "None:1", [[0, 0], [1, 0]]));
            var menu = main.OpenCompendiumSubmenu();
            await Wait(0.5);
            var button = menu.GetNode<Button>("RelicRpsStatsButton");
            var stopwatch = Stopwatch.StartNew(); button.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(0.1);
            Record("Open fixture ms including 100 ms frame wait=" + stopwatch.Elapsed.TotalMilliseconds);
            var dialog = menu.GetNode<AcceptDialog>("RelicRpsStatsDialog");
            Record("Layout: window=" + dialog.Size + "; scroll=" + Nodes(dialog).OfType<ScrollContainer>().Single().Size +
                "; panel=" + dialog.GetNode<Control>("StatsContents").Size);
            Check(Nodes(dialog).Count(n => n.Name.ToString().StartsWith("OpponentRow_")) == 25, "Only 25 of 10000 teammates rendered");
            var next = Nodes(dialog).OfType<Button>().Single(b => b.Text == "Next");
            var times = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                stopwatch.Restart(); next.EmitSignal(BaseButton.SignalName.Pressed); times.Add(stopwatch.Elapsed.TotalMilliseconds);
                Check(Nodes(dialog).Count(n => n.Name.ToString().StartsWith("OpponentRow_")) == 25, "Row cap after page " + (i + 2));
                await Wait(0.03);
            }
            Record("40 page updates ms=" + JsonSerializer.Serialize(times));
            var search = Nodes(dialog).OfType<LineEdit>().Single();
            search.Text = "09999";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Check(Nodes(dialog).Count(n => n.Name.ToString().StartsWith("OpponentRow_")) == 1, "Search covers final teammate beyond current page");
            search.Text = "";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Check(Nodes(dialog).Count(n => n.Name.ToString().StartsWith("OpponentRow_")) == 25, "Clearing search restores cap");
            await Wait(0.5);
            var panel = dialog.GetNode<Control>("StatsContents");
            foreach (var control in Nodes(panel).OfType<Control>().Take(15))
                Record($"Control {control.GetType().Name} {control.Name}: size={control.Size} min={control.GetCombinedMinimumSize()}");
            Check(panel.Size.Y <= dialog.Size.Y && Nodes(dialog).OfType<ScrollContainer>().Single().Size.Y < dialog.Size.Y,
                "Content and scrolling stay inside dialog height");
            if (DisplayServer.GetName() != "headless") NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(output + ".png");
            Check(Nodes(dialog).OfType<Label>().Any(l => l.Text.Contains("10000 W")), "Totals cover whole fixture");
            Record("COMPLETE fixture preview left open for native input QA");
        }
        catch (Exception e) { Record("FAIL " + e); }
    }
}
