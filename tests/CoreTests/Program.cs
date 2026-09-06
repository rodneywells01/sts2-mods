using RandomCharacterBlacklist;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var roster = new[] { (Id: "A", Available: true), (Id: "B", Available: true), (Id: "LOCKED", Available: false) };
var excluded = new HashSet<string> { "B", "REMOVED_MOD_CHARACTER" };
var pool = SelectionPool.Eligible(roster, item => item.Id, item => item.Available, excluded);
Check(pool.Count == 1 && pool[0].Id == "A", "Exclusions and availability must both apply.");
for (int i = 0; i < 1000; i++)
    Check(SelectionPool.TryPick(pool, Random.Shared.Next, out var chosen) && chosen.Id == "A", "One allowed character must always win.");
Check(!SelectionPool.TryPick(Array.Empty<string>(), _ => throw new Exception("RNG must not run for an empty pool"), out _), "Empty pool must block.");
var full = SelectionPool.Eligible(roster, item => item.Id, item => item.Available, new HashSet<string>());
Check(full.Count == 2, "An empty blacklist must still respect locked characters.");
for (int i = 0; i < full.Count; i++)
{
    int index = i;
    Check(SelectionPool.TryPick(full, _ => index, out var chosen) && chosen == full[i], "Every eligible index is reachable.");
}
string folder = Path.Combine(Path.GetTempPath(), "RCB-CoreTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
string path = Path.Combine(folder, "preferences.json");
try
{
    Check(Preferences.Load(path).ExcludedCharacters.Count == 0, "First run starts empty.");
    Check(Preferences.Load(path).Enabled, "Custom Random is enabled by default.");
    var preferences = new Preferences { ExcludedCharacters = excluded };
    preferences.Save(path);
    Check(Preferences.Load(path).ExcludedCharacters.SetEquals(excluded), "IDs must survive persistence, including absent mods.");
    preferences.ExcludedCharacters.Add("A");
    preferences.Save(path);
    Check(Preferences.Load(path).ExcludedCharacters.Contains("A"), "Atomic overwrite must work.");
    preferences.Enabled = false;
    preferences.Save(path);
    Check(!Preferences.Load(path).Enabled, "Disabled mode survives restart.");
    foreach (string invalid in new[] { "{", "null", "{\"SchemaVersion\":2}", "{\"ExcludedCharacters\":null}" })
    {
        File.WriteAllText(path, invalid);
        bool rejected = false;
        try { Preferences.Load(path); } catch { rejected = true; }
        Check(rejected && File.ReadAllText(path) == invalid, "Invalid preferences must remain intact for recovery.");
    }
}
finally { File.Delete(path); Directory.Delete(folder); }
Console.WriteLine($"PASS: {checks} core checks.");
