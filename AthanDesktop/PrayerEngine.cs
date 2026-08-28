using Batoulapps.Adhan;
using Batoulapps.Adhan.Internal;

namespace AthanDesktop;

/// <summary>
/// Everything shown in the daily list, in order. Sunrise is displayed because it
/// marks the end of Fajr's window, but it is not a prayer and must never raise
/// an athan - hence <see cref="Notifies"/>.
/// </summary>
public record Slot(string Name, Prayer Prayer, string LabelKey, bool Notifies = true)
{
    /// <summary>The prayer's name in the chosen language, looked up on demand.</summary>
    public string Label => Strings.Get(LabelKey);

    public static readonly Slot Fajr = new("FAJR", Prayer.FAJR, "fajr");
    public static readonly Slot Sunrise = new("SUNRISE", Prayer.SUNRISE, "sunrise", Notifies: false);
    public static readonly Slot Dhuhr = new("DHUHR", Prayer.DHUHR, "dhuhr");
    public static readonly Slot Asr = new("ASR", Prayer.ASR, "asr");
    public static readonly Slot Maghrib = new("MAGHRIB", Prayer.MAGHRIB, "maghrib");
    public static readonly Slot Isha = new("ISHA", Prayer.ISHA, "isha");

    public static readonly Slot[] All = { Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha };

    public static Slot? ByName(string name) => All.FirstOrDefault(s => s.Name == name);
}

public record Upcoming(Slot Slot, DateTime Time);

/// <summary>
/// Thin wrapper over the adhan library - the same one the Android app uses, so
/// the two agree to the minute. Everything is computed locally from latitude and
/// longitude; the app makes no network request of any kind.
/// </summary>
public class PrayerEngine(Settings settings)
{
    private CalculationParameters Params()
    {
        var method = Enum.TryParse<CalculationMethod>(settings.Method, out var m)
            ? m : CalculationMethod.NORTH_AMERICA;
        var parameters = method.GetParameters();
        parameters.Madhab = Enum.TryParse<Madhab>(settings.Madhab, out var md) ? md : Madhab.SHAFI;
        return parameters;
    }

    private PrayerTimes? TimesOn(DateTime date)
    {
        if (!settings.HasLocation) return null;
        var coordinates = new Coordinates(settings.Latitude, settings.Longitude);
        return new PrayerTimes(coordinates, DateComponents.From(date), Params());
    }

    /// <summary>
    /// The library works in UTC and hands back unspecified-kind values, so they
    /// have to be stamped before converting - left alone, .ToLocalTime() would
    /// treat them as already local and shift nothing. The user's manual offset
    /// is applied here, in one place, so the display and the alarm can never
    /// disagree.
    /// </summary>
    private DateTime Localise(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
            .AddMinutes(settings.AdjustmentMinutes);

    /// <summary>Today's times including sunrise, in order. Empty with no location.</summary>
    public List<(Slot Slot, DateTime Time)> Today() => On(DateTime.Now);

    public List<(Slot Slot, DateTime Time)> On(DateTime date)
    {
        var times = TimesOn(date);
        if (times is null) return new();
        var result = new List<(Slot, DateTime)>();
        foreach (var slot in Slot.All)
        {
            var t = times.TimeForPrayer(slot.Prayer);
            if (t.HasValue) result.Add((slot, Localise(t.Value)));
        }
        return result;
    }

    /// <summary>
    /// The next prayer strictly after <paramref name="now"/>. Rolls into
    /// tomorrow's Fajr once Isha has passed, which happens every single night -
    /// the reason this cannot simply scan today's list.
    /// </summary>
    public Upcoming? Next(DateTime? from = null)
    {
        var now = from ?? DateTime.Now;
        var today = TimesOn(now);
        if (today is null) return null;

        foreach (var slot in Slot.All.Where(s => s.Notifies))
        {
            var t = today.TimeForPrayer(slot.Prayer);
            if (t.HasValue)
            {
                var local = Localise(t.Value);
                if (local > now) return new Upcoming(slot, local);
            }
        }

        var tomorrow = TimesOn(now.AddDays(1));
        var fajr = tomorrow?.TimeForPrayer(Prayer.FAJR);
        return fajr.HasValue ? new Upcoming(Slot.Fajr, Localise(fajr.Value)) : null;
    }

    /// <summary>Bearing to the Kaaba, degrees clockwise from true north.</summary>
    public double? QiblaBearing() =>
        settings.HasLocation
            ? new Qibla(new Coordinates(settings.Latitude, settings.Longitude)).Direction
            : null;

    /// <summary>Great-circle distance to Mecca in kilometres.</summary>
    public double? MeccaDistanceKm()
    {
        if (!settings.HasLocation) return null;
        const double meccaLat = 21.4225, meccaLng = 39.8262, earthRadiusKm = 6371.0;
        double ToRad(double d) => d * Math.PI / 180.0;
        var dLat = ToRad(meccaLat - settings.Latitude);
        var dLng = ToRad(meccaLng - settings.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(settings.Latitude)) * Math.Cos(ToRad(meccaLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
