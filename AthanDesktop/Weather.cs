using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace AthanDesktop;

/// <summary>
/// The current temperature at the saved coordinates.
///
/// This is the <b>only</b> part of the app that touches the network. Everything
/// else - prayer times, the qibla, the athkar, the Ramadan calendar - is
/// computed on this PC and always will be. So it is built to be switched off
/// completely (<see cref="Settings.WeatherEnabled"/>) and to fail silently: if
/// the temperature cannot be had, the window simply does not show one.
///
/// Open-Meteo needs no API key, which is the reason it was chosen: a key
/// shipped inside a downloadable exe is a key anyone can extract.
/// </summary>
public static class Weather
{
    public record Reading(double Celsius, DateTime TakenAt);

    /// <summary>
    /// Half an hour. The temperature outside does not change faster than that
    /// in any way worth a request, and the window is opened many times a day.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static Reading? _cached;
    private static bool _inFlight;

    /// <summary>The last reading if it is still fresh, else null. Never blocks.</summary>
    public static Reading? Current =>
        _cached is { } r && DateTime.UtcNow - r.TakenAt < CacheFor ? r : null;

    /// <summary>Dropped when the feature is switched off, so nothing lingers.</summary>
    public static void Forget() => _cached = null;

    /// <summary>
    /// Fetches in the background and invokes <paramref name="onResult"/> when a
    /// reading arrives. Does nothing when the user has not yet answered the
    /// first-run notice, when the feature is off, when there is no location, or
    /// when a fetch is already running.
    /// </summary>
    public static async void Refresh(Settings settings, Action<Reading> onResult)
    {
        // Nothing leaves this PC until the first-run notice has been answered.
        if (!settings.WeatherNoticeSeen) return;
        if (!settings.WeatherEnabled || !settings.HasLocation) return;
        if (Current is not null || _inFlight) return;

        _inFlight = true;
        try
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast" +
                "?latitude={0:0.####}&longitude={1:0.####}&current=temperature_2m",
                settings.Latitude, settings.Longitude);

            var body = await Http.GetStringAsync(url).ConfigureAwait(true);
            var celsius = JsonDocument.Parse(body)
                .RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();

            _cached = new Reading(celsius, DateTime.UtcNow);
            onResult(_cached);
        }
        catch
        {
            // Deliberately silent. This is decoration on a prayer-times window;
            // no connection is a normal state, not an error worth a dialog.
        }
        finally
        {
            _inFlight = false;
        }
    }

    /// <summary>Formatted for display, in the user's chosen unit.</summary>
    public static string Format(Reading reading, bool fahrenheit) =>
        fahrenheit
            ? $"{Math.Round(reading.Celsius * 9 / 5 + 32)}°F"
            : $"{Math.Round(reading.Celsius)}°C";
}
