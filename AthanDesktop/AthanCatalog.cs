using System.IO;
using System.Reflection;
using System.Text;

namespace AthanDesktop;

public record AthanSound(
    /// <summary>Catalogue key, "category/file.mp3". This is what gets stored.</summary>
    string Key,
    string Label,
    string Duration,
    string Category,
    string CategoryLabel,
    string Reciter,
    string Source);

/// <summary>
/// The bundled recordings, read from resources embedded in the exe rather than
/// from files beside it - that is what makes a single downloadable file possible.
/// Built from the same index.tsv the Android app ships, so both platforms show
/// the same names, the same durations and the same credits.
/// </summary>
public class AthanCatalog
{
    private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

    /// <summary>Manifest resource name for each "category/file.mp3" key.</summary>
    private readonly Dictionary<string, string> _resources = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AthanSound> Fajr { get; }
    public IReadOnlyList<AthanSound> General { get; }
    public IReadOnlyList<AthanSound> All { get; }

    public AthanCatalog()
    {
        // Embedding flattens "Assets\athan\egyptian\023.mp3" into the resource
        // name "AthanDesktop.Assets.athan.egyptian.023.mp3", so the folder is
        // lost as a separator. The last three dot-parts are category, stem and
        // extension, which is enough to rebuild the catalogue key. This holds
        // only because no recording's filename contains a dot.
        foreach (var name in Asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = name.Split('.');
            if (parts.Length < 3) continue;
            _resources[$"{parts[^3]}/{parts[^2]}.{parts[^1]}"] = name;
        }

        var index = ReadIndex();
        var sounds = new List<AthanSound>();

        // The app's own recordings first, then alphabetical: they are the ones
        // with no third party behind them.
        foreach (var category in index.Keys.Select(k => k.Split('/')[0]).Distinct()
                     .OrderBy(c => c == "developer" ? 0 : 1).ThenBy(c => c))
        {
            var entries = index.Where(e => e.Key.StartsWith(category + "/", StringComparison.Ordinal))
                .OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
            var display = DisplayName(category);
            for (var i = 0; i < entries.Count; i++)
            {
                var (key, entry) = (entries[i].Key, entries[i].Value);
                sounds.Add(new AthanSound(
                    Key: key,
                    // The recording's own title names the reciter, which is far
                    // more use than "Egyptian 3". Untagged files - the app's own
                    // recordings - keep the numbered fallback.
                    Label: !string.IsNullOrWhiteSpace(entry.Title)
                        ? entry.Title
                        : entries.Count == 1 ? display : $"{display} {i + 1}",
                    Duration: entry.Seconds is int s ? $"{s / 60}:{s % 60:00}" : "",
                    Category: category,
                    CategoryLabel: display,
                    Reciter: entry.Title,
                    Source: entry.Source));
            }
        }

        All = sounds;
        Fajr = sounds.Where(s => s.Category == "fajr").ToList();
        General = sounds.Where(s => s.Category != "fajr").ToList();
    }

    /// <summary>
    /// Defaults are matched on the catalogue key, not the label: labels come
    /// from the recordings' own tags, so a label match would break the moment a
    /// tag changed and silently hand a fresh install someone else's recording.
    /// These are the two the author chose.
    /// </summary>
    public string? DefaultGeneral =>
        General.FirstOrDefault(s => s.Key == "kuwait/001.mp3")?.Key ?? General.FirstOrDefault()?.Key;

    public string? DefaultFajr =>
        Fajr.FirstOrDefault(s => s.Key == "fajr/168410.mp3")?.Key ?? Fajr.FirstOrDefault()?.Key;

    public string? ResolveGeneral(Settings s) => Resolve(s.OtherSound, General, DefaultGeneral);

    public string? ResolveFajr(Settings s) => Resolve(s.FajrSound, Fajr, DefaultFajr);

    private static string? Resolve(string stored, IReadOnlyList<AthanSound> bundled, string? fallback)
    {
        if (string.IsNullOrEmpty(stored)) return fallback;
        // An absolute path is a file the user chose from their own disk.
        if (Path.IsPathRooted(stored)) return File.Exists(stored) ? stored : fallback;
        return bundled.Any(b => b.Key == stored) ? stored : fallback;
    }

    public string LabelFor(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "Athan";
        if (Path.IsPathRooted(key)) return Path.GetFileNameWithoutExtension(key);
        return All.FirstOrDefault(s => s.Key == key)?.Label ?? "Athan";
    }

    /// <summary>
    /// A readable stream for a bundled key, or for a file the user picked. The
    /// caller owns it. Bundled audio is never written to disk.
    /// </summary>
    public Stream? Open(string key)
    {
        if (Path.IsPathRooted(key))
            return File.Exists(key) ? File.OpenRead(key) : null;
        return _resources.TryGetValue(key, out var resource) ? Asm.GetManifestResourceStream(resource) : null;
    }

    public IEnumerable<IGrouping<string, AthanSound>> ByCategory() => All.GroupBy(s => s.CategoryLabel);

    private record Entry(int? Seconds, string Title, string Source);

    private static Dictionary<string, Entry> ReadIndex()
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var name = Asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("index.tsv", StringComparison.OrdinalIgnoreCase));
        if (name is null) return result;
        using var stream = Asm.GetManifestResourceStream(name);
        if (stream is null) return result;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            result[$"{parts[0]}/{parts[1]}"] = new Entry(
                int.TryParse(parts[2].Trim(), out var secs) ? secs : null,
                parts.Length > 3 ? parts[3].Trim() : "",
                parts.Length > 4 ? parts[4].Trim() : "");
        }
        return result;
    }

    private static string DisplayName(string category) => category switch
    {
        "developer" => "Developer athan",
        "emarat" => "Emirates",
        "various" => "Various reciters",
        "fajr" => "Fajr athan",
        _ => char.ToUpper(category[0]) + category[1..],
    };
}
