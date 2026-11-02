using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace RelicRpsStats;

[ModInitializer(nameof(Initialize))]
public static class Mod
{
    public const string Id = "RelicRpsStats";
    internal static StatsBook Book = new();
    internal static StatsStore Store = null!;
    internal static string? LoadError;
    internal static string? SaveError;
    internal static Task PendingSave = Task.CompletedTask;
    private static readonly ConditionalWeakTable<NTreasureRoomRelicCollection, StatsPanel> Panels = new();
    [ThreadStatic] private static AwardCapture? capture;
    private static bool revealing;
    private sealed record AwardCapture(TreasureRoomRelicSynchronizer Sync, string Owner, string Platform,
        string RoomKey, List<Contest> Contests);

    public static void Initialize()
    {
        Store = new StatsStore(Path.Combine(OS.GetUserDataDir(), Id, "stats.json"));
        try { Book = Store.Load(); }
        catch (Exception e)
        {
            LoadError = "Saved stats could not be read. Recording is paused; the original file is preserved.";
            GD.PrintErr($"[{Id}] Load: {e}");
        }
        var harmony = new Harmony("rodneywells.relicrpsstats");
        harmony.Patch(AccessTools.Method(typeof(TreasureRoomRelicSynchronizer), "AwardRelics"),
            prefix: Hook(nameof(BeginAward)), postfix: Hook(nameof(EndAward)), finalizer: Hook(nameof(ClearAward)));
        harmony.Patch(AccessTools.Method(typeof(RelicPickingResult), nameof(RelicPickingResult.GenerateRelicFight)),
            postfix: Hook(nameof(CaptureFight)));
        harmony.Patch(AccessTools.Method(typeof(NTreasureRoomRelicCollection), "InitializeRelics"), postfix: Hook(nameof(TreasureReady)));
        harmony.Patch(AccessTools.Method(typeof(NTreasureRoomRelicCollection), "AnimateRelicAwards"), postfix: Hook(nameof(AnimateAwards)));
        harmony.Patch(AccessTools.Method(typeof(NCompendiumSubmenu), "_Ready"), postfix: Hook(nameof(CompendiumReady)));
        GD.Print($"[{Id}] 0.1.0 loaded. Observing native awards; stats: {OS.GetUserDataDir()}/{Id}/stats.json");
    }

    private static HarmonyMethod Hook(string name) => new(typeof(Mod).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!);
    private static void Safe(Action action)
    { try { action(); } catch (Exception e) { GD.PrintErr($"[{Id}] {e}"); } }

    private static void BeginAward(TreasureRoomRelicSynchronizer __instance)
    {
        capture = null;
        Safe(() =>
        {
            var manager = RunManager.Instance;
            // Ignore singleplayer, fake multiplayer and replays. Only the active synchronizer is authoritative.
            if (LoadError != null || manager.NetService?.Type is not (NetGameType.Host or NetGameType.Client) ||
                !ReferenceEquals(manager.TreasureRoomRelicSynchronizer, __instance)) return;
            var run = manager.DebugOnlyGetState();
            if (run == null || run.Players.Count < 2) return;
            string platform = manager.NetService.Platform.ToString();
            string owner = platform + ":" + manager.NetService.NetId;
            long start = (long)AccessTools.Field(typeof(RunManager), "_startTime").GetValue(manager)!;
            string roster = string.Join(",", run.Players.Select(p => p.NetId).Order());
            string room = $"{platform}/{start}/{run.Rng.Seed}/{roster}/{run.CurrentActIndex}/{run.TotalFloor}/{run.CurrentMapCoord}/{run.CurrentRoomCount}";
            capture = new AwardCapture(__instance, owner, platform, room, new());
        });
    }

    private static void CaptureFight(RelicPickingResult __result)
    {
        Safe(() =>
        {
            var current = capture;
            if (current == null || __result.type != RelicPickingResultType.FoughtOver || __result.fight == null || __result.player == null) return;
            var fight = __result.fight;
            string[] players = fight.playersInvolved.Select(p => current.Platform + ":" + p.NetId).ToArray();
            if (!players.Contains(current.Owner)) return;
            int index = current.Sync.CurrentRelics!.ToList().IndexOf(__result.relic);
            if (index < 0) return;
            var platform = RunManager.Instance.NetService.Platform;
            string[] names = fight.playersInvolved.Select(p => PlatformUtil.GetPlayerNameRaw(platform, p.NetId)).ToArray();
            current.Contests.Add(new Contest(StatsBook.Key(current.RoomKey + "/" + index + "/" + __result.relic.Id),
                current.Owner, players, names, current.Platform + ":" + __result.player.NetId,
                fight.rounds.Select(r => r.moves.Select(m => m.HasValue ? (int?)m.Value : null).ToArray()).ToArray()));
        });
    }

    private static void EndAward()
    {
        Safe(() =>
        {
            bool changed = false;
            foreach (var contest in capture?.Contests ?? []) changed |= Book.Add(contest);
            if (changed) PendingSave = Persist(Book.Events.ToArray());
        });
    }

    private static async Task Persist(Contest[] snapshot)
    {
        try { await Store.SaveAsync(snapshot); SaveError = null; }
        catch (Exception e)
        {
            SaveError = "Stats are in memory only. Saving failed; check file permissions and disk space. Next result retries.";
            GD.PrintErr($"[{Id}] Save: {e}");
        }
    }

    // A finalizer always clears observation context and returns the original exception unchanged.
    private static Exception? ClearAward(Exception? __exception) { capture = null; return __exception; }

    private static void TreasureReady(NTreasureRoomRelicCollection __instance)
    {
        Safe(() =>
        {
            if (RunManager.Instance.DebugOnlyGetState()?.Players.Count < 2) return;
            Panels.GetValue(__instance, screen => new StatsPanel(screen)).Refresh();
        });
    }

    private static void AnimateAwards(NTreasureRoomRelicCollection __instance, ref Task __result)
    {
        revealing = true;
        __result = AfterAnimation(__result, __instance);
    }

    private static async Task AfterAnimation(Task native, NTreasureRoomRelicCollection screen)
    {
        try { await native; }
        finally
        {
            revealing = false;
            Safe(() =>
            {
                if (GodotObject.IsInstanceValid(screen) && screen.IsInsideTree() && Panels.TryGetValue(screen, out var panel)) panel.Refresh();
            });
        }
    }

    private static void CompendiumReady(NCompendiumSubmenu __instance)
        => Safe(() => StatsPanel.AddCompendiumButton(__instance));

    internal static string? CurrentOwner()
    {
        var net = RunManager.Instance.NetService;
        if (net?.Type is NetGameType.Host or NetGameType.Client) return net.Platform + ":" + net.NetId;
        var platform = PlatformUtil.PrimaryPlatform;
        return platform + ":" + PlatformUtil.GetLocalPlayerId(platform);
    }

    internal static void ShowStats(Control parent)
    {
        if (revealing) return; // Never reveal a precomputed winner during the native animation.
        Safe(() => new StatsDialog(parent, Book, CurrentOwner(), LoadError ?? SaveError));
    }
}
