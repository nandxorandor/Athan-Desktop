using System.IO;
using System.Reflection;
using System.Text;

namespace AthanDesktop;

public record City(string Name, string Country, double Latitude, double Longitude)
{
    public string Display => $"{Name}, {Country}";
}

/// <summary>
/// A bundled list of cities, so setting a location needs no network. A desktop
/// has no GPS, and geocoding a typed city name would mean calling out to a
/// service - which would break the promise that this app never touches the
/// network. Anywhere not listed can be entered as coordinates directly.
/// </summary>
public static class Cities
{
    private static readonly Lazy<IReadOnlyList<City>> Loaded = new(Read);

    public static IReadOnlyList<City> All => Loaded.Value;

    public static IEnumerable<City> Search(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return All.Take(40);
        // Name matches first, then country matches: someone typing "Egypt"
        // wants the Egyptian cities, but someone typing "Cairo" wants Cairo top.
        return All.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Concat(All.Where(c => !c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                   && c.Country.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(60);
    }

    private static IReadOnlyList<City> Read()
    {
        var result = new List<City>();
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("cities.tsv", StringComparison.OrdinalIgnoreCase));
        if (name is null) return result;
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return result;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            var p = line.Split('\t');
            if (p.Length < 4) continue;
            if (double.TryParse(p[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(p[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                result.Add(new City(p[0], p[1], lat, lng));
            }
        }
        return result;
    }
}
