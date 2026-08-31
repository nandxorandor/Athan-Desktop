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
    /// <summary>
    /// A reading. <c>Code</c> is the WMO weather code for current conditions;
    /// <c>RainComing</c> is set when the next few hours hold rain or snow that
    /// is not already falling.
    /// </summary>
    public record Reading(double Celsius, DateTime TakenAt, int Code = -1, bool RainComing = false)
    {
        public string Symbol => WeatherSymbol.For(Code, RainComing);
    }

    /// <summary>
    /// Half an hour. The temperature outside does not change faster than that
    /// in any way worth a request, and the window is opened many times a day.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);

    /// <summary>How far ahead the umbrella looks. Beyond this it is not "soon".</summary>
    private const int RainWindowHours = 3;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static Reading? _cached;
    private static bool _inFlight;

    /// <summary>The last reading if it is still fresh, else null. Never blocks.</summary>
    /// <summary>
    /// The last reading, fresh or not. A temperature half an hour old is still
    /// worth more than a blank space: the box used to vanish the moment the
    /// cache expired and stay gone until something else forced a refresh, so an
    /// app left open for a few hours simply lost its temperature.
    /// </summary>
    public static Reading? Last => _cached;

    /// <summary>The reading if it is still fresh; null once it needs refetching.</summary>
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
                "?latitude={0:0.####}&longitude={1:0.####}" +
                "&current=temperature_2m,weather_code" +
                // Six hours is far enough ahead to be worth knowing and near
                // enough to still be true. One request, not two.
                "&hourly=weather_code&forecast_hours=6",
                settings.Latitude, settings.Longitude);

            var body = await Http.GetStringAsync(url).ConfigureAwait(true);
            var root = JsonDocument.Parse(body).RootElement;
            var current = root.GetProperty("current");
            var celsius = current.GetProperty("temperature_2m").GetDouble();
            var code = current.TryGetProperty("weather_code", out var c) ? c.GetInt32() : -1;

            // Only worth flagging when nothing is falling already: an umbrella
            // beside a rain cloud tells you nothing new.
            _cached = new Reading(celsius, DateTime.UtcNow, code,
                !WeatherSymbol.IsWet(code) && RainAhead(root));
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

    /// <summary>
    /// True if rain is close enough and heavy enough to be worth an umbrella.
    ///
    /// This used to flag any wet code anywhere in the six-hour window, which put
    /// an umbrella over a cloudy afternoon because one hour near the end of the
    /// forecast showed light drizzle. "Soon" has to mean soon: only the next
    /// three hours count, and the lightest drizzle codes (51-55) do not - they
    /// are the ones that most often never arrive.
    /// </summary>
    private static bool RainAhead(JsonElement root)
    {
        try
        {
            var hours = 0;
            foreach (var hour in root.GetProperty("hourly").GetProperty("weather_code").EnumerateArray())
            {
                if (hours++ >= RainWindowHours) break;
                var code = hour.GetInt32();
                if (WeatherSymbol.IsWet(code) && code is not (>= 51 and <= 55)) return true;
            }
        }
        catch
        {
            // The forecast is a bonus on top of the reading; losing it is not
            // a reason to lose the temperature too.
        }
        return false;
    }

    /// <summary>Formatted for display, in the user's chosen unit.</summary>
    public static string Format(Reading reading, bool fahrenheit) =>
        fahrenheit
            ? $"{Math.Round(reading.Celsius * 9 / 5 + 32)}°F"
            : $"{Math.Round(reading.Celsius)}°C";
}
