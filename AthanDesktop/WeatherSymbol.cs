namespace AthanDesktop;

/// <summary>
/// One character for the sky, from a WMO weather code.
///
/// Kept in its own file, and matching <c>Weather.Reading.symbol</c> in the
/// Android app character for character: the two platforms must not disagree
/// about what the weather looks like.
/// </summary>
public static class WeatherSymbol
{
    /// <summary>Drizzle, rain, showers, snow or thunderstorm — anything falling.</summary>
    public static bool IsWet(int code) =>
        code is (>= 51 and <= 67) or (>= 71 and <= 86) or (>= 95 and <= 99);

    /// <summary>
    /// WMO codes group cleanly: 0 clear, 1-3 increasing cloud, 45-48 fog,
    /// 51-67 drizzle and rain, 71-77 snow, 80-82 showers, 95-99 thunderstorm.
    ///
    /// The umbrella is deliberately not the rain cloud: it says "not now, but
    /// soon", which a rain icon under a clear sky could not.
    /// </summary>
    public static string For(int code, bool rainComing) => code switch
    {
        < 0 => "",
        >= 95 and <= 99 => "⛈️",
        (>= 71 and <= 77) or 85 or 86 => "❄️",
        (>= 51 and <= 67) or (>= 80 and <= 82) => "🌧️",
        >= 45 and <= 48 => "🌫️",
        3 => rainComing ? "🌂" : "☁️",
        1 or 2 => rainComing ? "🌂" : "🌤️",
        _ => rainComing ? "🌂" : "☀️",
    };
}
