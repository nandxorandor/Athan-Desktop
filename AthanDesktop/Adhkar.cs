using System.IO;
using System.Reflection;
using System.Text;

namespace AthanDesktop;

/// <summary>One remembrance: what is said, how many times, and who narrated it.</summary>
public record Dhikr(string Text, string Repeat, string Source);

/// <summary>The two sittings.</summary>
public enum AdhkarSitting
{
    Morning,
    Evening,
}

/// <summary>
/// The morning and evening athkar, read from the same three-column TSV the
/// Android app ships. The file is the shared artefact between the two
/// platforms: a religious text should be reviewable as a plain file, and
/// correcting a letter must not mean editing code on two operating systems.
/// </summary>
public static class Adhkar
{
    private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

    public static IReadOnlyList<Dhikr> Load(AdhkarSitting sitting)
    {
        var wanted = sitting == AdhkarSitting.Morning ? "morning.tsv" : "evening.tsv";
        var name = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(wanted, StringComparison.OrdinalIgnoreCase));
        if (name is null) return [];

        var result = new List<Dhikr>();
        using var stream = Asm.GetManifestResourceStream(name);
        if (stream is null) return result;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split('\t');
            var text = parts.ElementAtOrDefault(0)?.Trim() ?? "";
            if (text.Length == 0) continue;
            result.Add(new Dhikr(
                text,
                parts.ElementAtOrDefault(1)?.Trim() ?? "",
                parts.ElementAtOrDefault(2)?.Trim() ?? ""));
        }
        return result;
    }
}
