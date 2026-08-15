using System.Globalization;
using System.IO;
using System.Text;

namespace AthanDesktop;

/// <summary>One day of Ramadan, with the times people actually plan around.</summary>
public record RamadanDay(
    int DayOfRamadan,
    DateTime Date,
    DateTime Fajr,
    DateTime Sunrise,
    DateTime Dhuhr,
    DateTime Asr,
    DateTime Maghrib,
    DateTime Isha);

/// <summary>
/// The whole month in one table. Ramadan is the one time of year when people
/// want the times for every day at once rather than just today's - the fast is
/// planned around suhoor and iftar the night before - so this exists to be
/// printed and stuck on a fridge, not scrolled.
/// </summary>
public static class RamadanCalendar
{
    private static readonly UmAlQuraCalendar Hijri = new();

    /// <summary>Ramadan is the ninth month.</summary>
    private const int RamadanMonth = 9;

    public static int CurrentHijriYear()
    {
        try { return Hijri.GetYear(DateTime.Now); }
        catch { return 0; }
    }

    /// <summary>
    /// The Gregorian date on which 1 Ramadan of <paramref name="hijriYear"/>
    /// falls, or null if it is outside the Umm al-Qura tables (roughly 1900-2077).
    /// </summary>
    public static DateTime? FirstDay(int hijriYear)
    {
        try { return Hijri.ToDateTime(hijriYear, RamadanMonth, 1, 0, 0, 0, 0); }
        catch { return null; }
    }

    public static int DaysIn(int hijriYear)
    {
        try { return Hijri.GetDaysInMonth(hijriYear, RamadanMonth); }
        catch { return 30; }
    }

    /// <summary>
    /// The Hijri year whose Ramadan is worth offering right now: this year's if
    /// it has not finished, otherwise next year's. Returns 0 when unavailable.
    /// </summary>
    public static int UpcomingHijriYear()
    {
        var year = CurrentHijriYear();
        if (year == 0) return 0;
        var first = FirstDay(year);
        if (first is null) return 0;
        var last = first.Value.AddDays(DaysIn(year) - 1);
        return DateTime.Today > last.Date ? year + 1 : year;
    }

    /// <summary>
    /// True inside the window where the offer is welcome rather than noise:
    /// from <paramref name="leadDays"/> before the first fast until the last.
    /// </summary>
    public static bool IsSeason(int hijriYear, int leadDays = 14)
    {
        var first = FirstDay(hijriYear);
        if (first is null) return false;
        var last = first.Value.AddDays(DaysIn(hijriYear) - 1);
        return DateTime.Today >= first.Value.Date.AddDays(-leadDays) && DateTime.Today <= last.Date;
    }

    /// <summary>Every day of the month, computed with the user's own settings.</summary>
    public static List<RamadanDay> Build(int hijriYear, Settings settings)
    {
        var days = new List<RamadanDay>();
        var first = FirstDay(hijriYear);
        if (first is null || !settings.HasLocation) return days;

        var engine = new PrayerEngine(settings);
        var count = DaysIn(hijriYear);
        for (var i = 0; i < count; i++)
        {
            var date = first.Value.Date.AddDays(i);
            var times = engine.On(date);
            if (times.Count < 6) continue;

            DateTime At(Slot slot) => times.First(t => t.Slot == slot).Time;
            days.Add(new RamadanDay(
                i + 1, date,
                At(Slot.Fajr), At(Slot.Sunrise), At(Slot.Dhuhr),
                At(Slot.Asr), At(Slot.Maghrib), At(Slot.Isha)));
        }
        return days;
    }

    // ---- export ------------------------------------------------------------

    private static string T(DateTime t) => t.ToString("h:mm tt", CultureInfo.CurrentCulture);

    /// <summary>
    /// The whole month as a Word document, sized to land on <b>one page</b> -
    /// this is meant to be printed and stuck on a wall, and a timetable that
    /// spills onto a second sheet defeats the purpose. Everything below is
    /// tuned to that: 8pt body text, 1 cm margins, and row heights that leave
    /// 30 days plus a header and a footnote inside a single Letter page.
    /// </summary>
    public static string BuildDocx(int hijriYear, IReadOnlyList<RamadanDay> days, Settings settings)
    {
        const string accent = "1F7A4D";
        const string band = "F2F8F5";
        const int body = 16;    // half-points, so 8pt
        const int rowHeight = 200;

        var place = string.IsNullOrWhiteSpace(settings.CityName)
            ? $"{settings.Latitude:0.##}, {settings.Longitude:0.##}"
            : settings.CityName;
        var range = days.Count == 0
            ? ""
            : $"{days[0].Date:d MMMM yyyy} – {days[^1].Date:d MMMM yyyy}";
        var madhab = settings.Madhab == "HANAFI" ? "Hanafi" : "Standard";
        var adjustment = settings.AdjustmentMinutes != 0
            ? $"  ·  adjusted by {settings.AdjustmentMinutes:+#;-#;0} min"
            : "";

        var (pageWidth, pageHeight) = DocxWriter.Letter;
        const int margin = 567; // 1 cm
        var usable = pageWidth - margin * 2;

        // Ramadan, Date, Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha. The two the
        // fast turns on get the extra room, because their headings are longest.
        var weights = new[] { 8, 15, 16, 12, 12, 12, 16, 12 };
        var total = weights.Sum();
        var widths = weights.Select(w => usable * w / total).ToList();
        widths[^1] += usable - widths.Sum();

        var rows = new List<IReadOnlyList<DocxWriter.Cell>>
        {
            new[] { "Ramadan", "Date", "Suhoor ends", "Sunrise", "Dhuhr", "Asr", "Iftar", "Isha" }
                .Select(h => new DocxWriter.Cell(h) { Bold = true, Fill = accent, Colour = "FFFFFF" })
                .ToList(),
        };

        foreach (var d in days)
        {
            // Fridays shaded, and every other row banded, so the eye can track
            // across eight columns without a ruler.
            var fill = d.Date.DayOfWeek == DayOfWeek.Friday ? "E2F1E9"
                : d.DayOfRamadan % 2 == 0 ? band
                : null;
            rows.Add(new List<DocxWriter.Cell>
            {
                new(d.DayOfRamadan.ToString()) { Bold = true, Fill = fill, Centre = true },
                new(d.Date.ToString("ddd, d MMM")) { Fill = fill },
                new(T(d.Fajr)) { Bold = true, Colour = accent, Fill = fill },
                new(T(d.Sunrise)) { Fill = fill },
                new(T(d.Dhuhr)) { Fill = fill },
                new(T(d.Asr)) { Fill = fill },
                new(T(d.Maghrib)) { Bold = true, Colour = accent, Fill = fill },
                new(T(d.Isha)) { Fill = fill },
            });
        }

        var content = new StringBuilder();
        content.AppendLine(DocxWriter.Paragraph($"Ramadan {hijriYear} AH", 32, bold: true, colour: accent));
        content.AppendLine(DocxWriter.Paragraph($"{place}  ·  {range}", 17, colour: "5D6F66"));
        content.AppendLine(DocxWriter.Paragraph(
            $"{MethodName(settings.Method)}  ·  Asr: {madhab}{adjustment}", 15, colour: "5D6F66", spaceAfter: 140));
        content.AppendLine(DocxWriter.Table(widths, rows, body, rowHeight));
        content.AppendLine(DocxWriter.Paragraph(
            "Dates follow the Umm al-Qura calendar, which is calculated rather than sighted, so your local " +
            "mosque may begin or end the month a day either side. Check these times against your mosque's " +
            "timetable before relying on them.", 13, colour: "5D6F66", spaceAfter: 40));
        content.AppendLine(DocxWriter.Paragraph(
            "Generated by Athan for Windows · github.com/nandxorandor/Athan-Desktop", 13, colour: "8A9A92"));

        return DocxWriter.Document(content.ToString(), pageWidth, pageHeight, margin);
    }

    /// <summary>For anyone who would rather have it in a spreadsheet.</summary>
    public static string BuildCsv(IReadOnlyList<RamadanDay> days)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ramadan,Date,Weekday,Suhoor ends (Fajr),Sunrise,Dhuhr,Asr,Iftar (Maghrib),Isha");
        foreach (var d in days)
        {
            sb.AppendLine(string.Join(',',
                d.DayOfRamadan,
                d.Date.ToString("yyyy-MM-dd"),
                d.Date.ToString("dddd"),
                T(d.Fajr), T(d.Sunrise), T(d.Dhuhr), T(d.Asr), T(d.Maghrib), T(d.Isha)));
        }
        return sb.ToString();
    }

    /// <summary>UTF-8 with a BOM: without it Excel misreads a non-ASCII city name.</summary>
    public static void WriteCsv(string path, string csv) =>
        File.WriteAllText(path, csv, new UTF8Encoding(true));

    private static string MethodName(string method) => method switch
    {
        "NORTH_AMERICA" => "North America (ISNA)",
        "MUSLIM_WORLD_LEAGUE" => "Muslim World League",
        "EGYPTIAN" => "Egyptian General Authority",
        "UMM_AL_QURA" => "Umm al-Qura (Makkah)",
        "MOON_SIGHTING_COMMITTEE" => "Moonsighting Committee",
        _ => method.Replace('_', ' '),
    };
}
