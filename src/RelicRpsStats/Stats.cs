using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RelicRpsStats;

// Game-independent records. Player identity includes platform; names are display metadata only.
public sealed record Contest(string Key, string Owner, string[] Players, string[] Names,
    string Winner, int?[][] Rounds);

public sealed class Totals
{
    public long RelicWins, RelicLosses, OtherWinner;
    public long ThrowWins, ThrowLosses, ThrowTies;
    public long Contests => RelicWins + RelicLosses + OtherWinner;
    public string RelicRate => Percent(RelicWins, Contests);
    public string ThrowRate => Percent(ThrowWins, ThrowWins + ThrowLosses);
    private static string Percent(long wins, long total) => total == 0 ? "—" : $"{100.0 * wins / total:0.#}%";
}

public sealed class Opponent
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public Totals Totals { get; } = new();
}

public sealed class Profile
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public Totals Totals { get; } = new();
    public Dictionary<string, Opponent> Opponents { get; } = new(StringComparer.Ordinal);
}

public sealed class StatsBook
{
    public const int PageSize = 25;
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private readonly List<Contest> events = new();
    private readonly Dictionary<string, string> runKeys = new(StringComparer.Ordinal);
    public IReadOnlyList<Contest> Events => events;
    public IReadOnlyDictionary<string, string> RunKeys => runKeys;
    public Dictionary<string, Profile> Profiles { get; } = new(StringComparer.Ordinal);
    public static string Key(string identity) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

    // Native peers create different start timestamps; clients adopt the host's timestamp on reload.
    // Persist our own generation for the native save slot (seed + roster + local account).
    // Starting a new run explicitly rotates it; loading that slot reuses it.
    public string RunKey(string signature, bool newRun = false)
    {
        string key = Key(signature);
        if (newRun || !runKeys.ContainsKey(key)) runKeys[key] = Guid.NewGuid().ToString("N");
        return runKeys[key];
    }

    public void RestoreRunKeys(Dictionary<string, string>? saved)
    {
        foreach (var pair in saved ?? [])
        {
            if (pair.Key.Length != 64 || !pair.Key.All(Uri.IsHexDigit) || !Guid.TryParseExact(pair.Value, "N", out _))
                throw new InvalidDataException("Invalid saved run identity.");
            runKeys.Add(pair.Key, pair.Value);
        }
    }

    public bool Add(Contest contest)
    {
        Validate(contest);
        if (!keys.Add(contest.Owner + ":" + contest.Key)) return false;
        events.Add(contest);
        if (!Profiles.TryGetValue(contest.Owner, out var profile))
            Profiles.Add(contest.Owner, profile = new Profile { Id = contest.Owner });
        int local = Array.IndexOf(contest.Players, contest.Owner);
        profile.Name = CleanName(contest.Names[local]);
        AddResult(profile.Totals, contest.Winner == contest.Owner ? 1 : -1);
        for (int i = 0; i < contest.Players.Length; i++)
        {
            if (i == local) continue;
            string id = contest.Players[i];
            if (!profile.Opponents.TryGetValue(id, out var opponent))
                profile.Opponents.Add(id, opponent = new Opponent { Id = id });
            opponent.Name = CleanName(contest.Names[i]);
            AddResult(opponent.Totals, contest.Winner == contest.Owner ? 1 : contest.Winner == id ? -1 : 0);
        }
        foreach (var round in contest.Rounds)
        {
            if (round[local] is not int move) continue; // Eliminated players do not throw again.
            var distinct = round.Where(m => m.HasValue).Select(m => m!.Value).Distinct().ToArray();
            int? losing = distinct.Length == 2 ? ((distinct[0] + 1) % 3 == distinct[1] ? distinct[0] : distinct[1]) : null;
            AddThrow(profile.Totals, losing == null ? 0 : move == losing ? -1 : 1);
            for (int i = 0; i < round.Length; i++)
            {
                if (i == local || round[i] == null) continue;
                // Three different moves tie the entire native round. Otherwise compare shared throws.
                int outcome = losing == null || round[i] == move ? 0 : move == losing ? -1 : 1;
                AddThrow(profile.Opponents[contest.Players[i]].Totals, outcome);
            }
        }
        return true;
    }

    public static string CleanName(string name) => new(name.Where(c => !char.IsControl(c)).Take(80).ToArray());
    private static void AddResult(Totals t, int outcome)
    { if (outcome > 0) t.RelicWins++; else if (outcome < 0) t.RelicLosses++; else t.OtherWinner++; }
    private static void AddThrow(Totals t, int outcome)
    { if (outcome > 0) t.ThrowWins++; else if (outcome < 0) t.ThrowLosses++; else t.ThrowTies++; }

    public static void Validate(Contest c)
    {
        if (c == null || c.Key == null || c.Key.Length != 64 || !c.Key.All(Uri.IsHexDigit) ||
            c.Players == null || c.Names == null || c.Rounds == null || c.Players.Length is < 2 or > 4 ||
            c.Players.Any(string.IsNullOrWhiteSpace) || c.Players.Distinct(StringComparer.Ordinal).Count() != c.Players.Length ||
            c.Names.Length != c.Players.Length || c.Names.Any(n => n == null || n.Length > 1024) ||
            !c.Players.Contains(c.Owner) || !c.Players.Contains(c.Winner) || c.Rounds.Length is < 1 or > 10000)
            throw new InvalidDataException("Invalid RPS contest record.");
        var active = Enumerable.Repeat(true, c.Players.Length).ToArray();
        foreach (var round in c.Rounds)
        {
            if (round == null || round.Length != active.Length || active.Count(a => a) < 2)
                throw new InvalidDataException("Invalid RPS round.");
            for (int i = 0; i < active.Length; i++)
                if (active[i] != round[i].HasValue || round[i] is < 0 or > 2)
                    throw new InvalidDataException("Invalid RPS move or elimination.");
            var moves = round.Where(m => m.HasValue).Select(m => m!.Value).Distinct().ToArray();
            if (moves.Length == 2)
            {
                int losing = (moves[0] + 1) % 3 == moves[1] ? moves[0] : moves[1];
                for (int i = 0; i < active.Length; i++) if (round[i] == losing) active[i] = false;
            }
        }
        if (active.Count(a => a) != 1 || !active[Array.IndexOf(c.Players, c.Winner)])
            throw new InvalidDataException("RPS winner does not match rounds.");
    }
}

public sealed record StatsFile(int SchemaVersion, Contest[] Events, Dictionary<string, string>? Runs = null);

// Serialized background writes keep disk I/O off the game thread. Failed writes retain the old file.
public sealed class StatsStore(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public const int MaxBytes = 64 * 1024 * 1024;
    public StatsBook Load()
    {
        var book = new StatsBook();
        if (!File.Exists(path)) return book;
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Stats file exceeds 64 MiB.");
        var file = JsonSerializer.Deserialize<StatsFile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty stats file.");
        if (file.SchemaVersion != 1 || file.Events == null) throw new InvalidDataException("Unsupported stats schema.");
        book.RestoreRunKeys(file.Runs);
        foreach (var contest in file.Events)
            if (!book.Add(contest)) throw new InvalidDataException("Duplicate saved contest.");
        return book;
    }

    public async Task SaveAsync(Contest[] snapshot, Dictionary<string, string>? runs = null)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        string temp = path + ".tmp";
        try
        {
            await Task.Run(() =>
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new StatsFile(1, snapshot, runs));
                if (bytes.Length > MaxBytes) throw new InvalidDataException("Stats file exceeds 64 MiB.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
