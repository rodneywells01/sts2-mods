using System.Text.Json;

namespace RandomCharacterBlacklist;

public enum RevealMode { Immediately, AtLockIn }

public sealed class Preferences
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    // Missing in older settings: preserve the existing immediate-reveal behavior.
    public RevealMode Reveal { get; set; } = RevealMode.Immediately;
    public HashSet<string> ExcludedCharacters { get; set; } = new(StringComparer.Ordinal);

    public static Preferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The preferences file is empty.");
        if (value.SchemaVersion != 1 || !Enum.IsDefined(value.Reveal) || value.ExcludedCharacters is null || value.ExcludedCharacters.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Unsupported or invalid preferences file.");
        value.ExcludedCharacters = new(value.ExcludedCharacters, StringComparer.Ordinal);
        return value;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class SelectionPool
{
    public static List<T> Eligible<T>(IEnumerable<T> roster, Func<T, string> id, Func<T, bool> available, ISet<string> excluded)
        => roster.Where(item => available(item) && !excluded.Contains(id(item))).ToList();

    public static bool TryPick<T>(IReadOnlyList<T> pool, Func<int, int> nextIndex, out T? selected)
    {
        if (pool.Count == 0) { selected = default; return false; }
        selected = pool[nextIndex(pool.Count)];
        return true;
    }
}
