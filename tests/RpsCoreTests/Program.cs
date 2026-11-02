using RelicRpsStats;
using System.Text.Json;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Reject(Action action, string message)
{ try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
Contest Fight(string id, string owner, string[] players, string winner, params int?[][] rounds)
    => new(StatsBook.Key(id), owner, players, players.Select(p => "Name " + p).ToArray(), winner, rounds);
var book = new StatsBook();
var win = Fight("one", "Steam:1", ["Steam:1", "Steam:2"], "Steam:1", [0, 0], [1, 0]);
Check(book.Add(win), "First result recorded");
Check(!book.Add(win), "Duplicate ignored");
var t = book.Profiles["Steam:1"].Totals;
Check(t.RelicWins == 1 && t.RelicLosses == 0 && t.ThrowWins == 1 && t.ThrowTies == 1, "Tie then win totals");
Check(t.ThrowRate == "100%" && t.RelicRate == "100%", "Ties excluded from decisive denominator");
Check(book.Add(win with { Owner = "Steam:2" }), "Another owner's record is separate");
Check(book.Profiles["Steam:2"].Totals.RelicLosses == 1, "Opponent local perspective");
var multi = Fight("three", "Steam:1", ["Steam:1", "Steam:2", "Steam:3"], "Steam:3",
    [0, 1, 2], [0, 1, 1], [null, 0, 1]);
book.Add(multi);
Check(t.ThrowTies == 2 && t.ThrowLosses == 1 && t.ThrowWins == 1, "Three-way tie; no throws after elimination");
var opponent = book.Profiles["Steam:1"].Opponents["Steam:2"].Totals;
Check(opponent.RelicWins == 1 && opponent.OtherWinner == 1 && opponent.RelicLosses == 0, "Third-party winner is distinct");
Check(opponent.ThrowTies == 2 && opponent.ThrowWins == 1 && opponent.ThrowLosses == 1, "Shared throw outcomes");
book.Add(Fight("four", "Steam:1", ["Steam:1", "Steam:2", "Steam:3", "Steam:4"], "Steam:1",
    [2, 1, 2, 1], [0, null, 2, null]));
Check(t.RelicWins == 2 && t.RelicLosses == 1 && t.ThrowWins == 3, "Four-player progressive elimination");
Check(book.Profiles["Steam:1"].Opponents["Steam:2"].Totals.ThrowWins == 2, "Only simultaneous throws count against opponent");
Check(new Totals().RelicRate == "—", "Empty denominator");
for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++)
{
    if (a == b) continue;
    string winner = (a + 1) % 3 == b ? "b" : "a";
    var sample = new StatsBook(); sample.Add(Fight($"{a}/{b}", "a", ["a", "b"], winner, [a, b]));
    Check(sample.Profiles["a"].Totals.ThrowWins == (winner == "a" ? 1 : 0), "All decisive move pairs");
}
Reject(() => book.Add(win with { Rounds = [[3, 0]] }), "Reject unknown move");
Reject(() => book.Add(win with { Winner = "missing" }), "Reject missing winner");
Reject(() => book.Add(win with { Rounds = [[0, 0]] }), "Reject unresolved tie");
Reject(() => book.Add(win with { Rounds = [[0, 1]], Winner = "Steam:1" }), "Reject wrong winner");
Reject(() => book.Add(win with { Players = ["Steam:1", "Steam:1"] }), "Reject duplicate identities");
Reject(() => book.Add(multi with { Rounds = [[0, 1, 1], [0, 0, 1]] }), "Reject eliminated player returning");
Reject(() => book.Add(win with { Rounds = [[1, 0], [1, 0]] }), "Reject extra rounds after final");
Check(StatsBook.CleanName("A\n\r\tB").Equals("AB"), "Control characters removed");
Check(StatsBook.CleanName(new string('x', 500)).Length == 80, "Names bounded");

string folder = Path.Combine(Path.GetTempPath(), "rps-tests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    string path = Path.Combine(folder, "stats.json");
    var store = new StatsStore(path);
    Check(store.Load().Events.Count == 0, "Missing file starts empty");
    await store.SaveAsync(book.Events.ToArray());
    var reloaded = store.Load();
    Check(reloaded.Events.Count == book.Events.Count && !reloaded.Add(win), "Restart preserves history and deduplication");
    Check(reloaded.Profiles["Steam:1"].Totals.RelicWins == 2, "Restart reconstructs totals");
    await Task.WhenAll(store.SaveAsync([win]), store.SaveAsync(book.Events.ToArray()));
    Check(store.Load().Events.Count == book.Events.Count, "Ordered writes cannot regress final snapshot");
    Check(File.Exists(path + ".bak"), "Atomic replacement retains previous file");
    string original = File.ReadAllText(path);
    using (var locked = new FileStream(path + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
    {
        try { await store.SaveAsync([win]); throw new Exception("Expected write failure"); }
        catch (IOException) { checks++; }
    }
    Check(File.ReadAllText(path) == original, "Failed write preserves saved file");
    await store.SaveAsync(book.Events.ToArray());
    Check(store.Load().Events.Count == book.Events.Count, "Recovery after failed write");
    foreach (string corrupt in new[] { "{broken", "null", "{\"SchemaVersion\":9,\"Events\":[]}", JsonSerializer.Serialize(new StatsFile(1, [win, win])) })
    {
        File.WriteAllText(path, corrupt);
        try { store.Load(); throw new Exception("Corrupt file accepted"); }
        catch (Exception e) when (e is InvalidDataException or JsonException) { checks++; }
        Check(File.ReadAllText(path) == corrupt, "Invalid file is not overwritten");
    }
}
finally { Directory.Delete(folder, true); }

var large = new StatsBook();
for (int i = 0; i < 10000; i++) large.Add(Fight("large" + i, "me", ["me", "peer" + i], "me", [1, 0]));
Check(large.Profiles["me"].Opponents.Count == 10000 && large.Profiles["me"].Totals.RelicWins == 10000, "Full dataset totals");
for (int page = 0; page < 400; page++)
    Check(large.Profiles["me"].Opponents.Values.Skip(page * StatsBook.PageSize).Take(StatsBook.PageSize).Count() == 25, "Page cap");
Console.WriteLine($"PASS {checks} RPS checks");
