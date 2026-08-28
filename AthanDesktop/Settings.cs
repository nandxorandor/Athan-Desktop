using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AthanDesktop;

/// <summary>
/// How the athan announces itself. No Vibrate - a PC has nothing to vibrate -
/// but a middle setting instead: the window still appears so you know the time
/// has come, without a recording playing over whatever you are doing.
/// </summary>
public enum AthanMode
{
    /// <summary>Window plus the recording.</summary>
    Sound,

    /// <summary>Window only, no audio.</summary>
    Popup,

    /// <summary>Nothing at all for this prayer.</summary>
    Silent,
}

/// <summary>
/// Everything persisted, as JSON under %APPDATA%\Athan. The single-file exe cannot
/// write to itself, and a user who moves the exe must keep their location and
/// prayer settings — so state deliberately does not live next to the binary.
/// </summary>
public class Settings
{
    public double Latitude { get; set; } = double.NaN;
    public double Longitude { get; set; } = double.NaN;
    public string CityName { get; set; } = "";

    /// <summary>North America (ISNA) to match the Android app, which was checked
    /// against local timetables; Muslim World League runs Fajr early here.</summary>
    public string Method { get; set; } = "NORTH_AMERICA";
    public string Madhab { get; set; } = "SHAFI";

    /// <summary>Applied to every computed time, in minutes, in one place.</summary>
    public int AdjustmentMinutes { get; set; }

    /// <summary>0-100, scaling this app's own output only.</summary>
    public int Volume { get; set; } = 100;

    public string FajrSound { get; set; } = "";
    public string OtherSound { get; set; } = "";

    /// <summary>Per prayer, keyed by slot name. Absent means Sound.</summary>
    public Dictionary<string, AthanMode> Modes { get; set; } = new();

    /// <summary>
    /// On by default: an app whose job is to call the prayers is useless if it
    /// only runs when you remember to open it.
    /// </summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>
    /// Whether the login launch also opens the main window. On by default: an
    /// app that comes up in the notification area only reads as not having
    /// started at all, and after a restart the day's times are the first thing
    /// wanted. Unticking it keeps the quiet start.
    /// </summary>
    public bool ShowWindowOnStartup { get; set; } = true;

    /// <summary>
    /// Whether the default above has been acted on. Without this the app would
    /// re-add the startup entry on every launch, silently overriding a user who
    /// deliberately turned it off.
    /// </summary>
    public bool StartupApplied { get; set; }

    /// <summary>Closing the window hides to the tray rather than quitting; shown once.</summary>
    public bool TrayNoticeSeen { get; set; }

    /// <summary>A short "be ready" popup before each prayer.</summary>
    public bool ReminderEnabled { get; set; }

    /// <summary>How many minutes before the prayer the heads-up appears.</summary>
    public int ReminderMinutes { get; set; } = 10;

    /// <summary>
    /// The du'aa said after the athan, played once the recording ends. On by
    /// default because it is the natural companion to the call, and one toggle
    /// away for anyone who would rather it did not.
    /// </summary>
    public bool AfterAthanDuaEnabled { get; set; } = true;

    /// <summary>Offer a month calendar as Ramadan comes round.</summary>
    public bool RamadanPromptEnabled { get; set; } = true;

    /// <summary>
    /// The Hijri year whose offer was dismissed. Stored as a year rather than a
    /// flag so "don't ask again" lapses on its own next Ramadan, instead of
    /// silently switching the feature off for good.
    /// </summary>
    public int RamadanPromptDismissedYear { get; set; }

    /// <summary>
    /// "en" or "ar"; empty until the user picks one, which lets a fresh install
    /// follow Windows' own language instead of guessing English.
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// The temperature in the header. On by default, and the one switch that
    /// takes the app back to making no network requests at all.
    /// </summary>
    public bool WeatherEnabled { get; set; } = true;

    /// <summary>
    /// Whether the first-run notice about the temperature has been shown. It is
    /// asked once, before any coordinates leave this PC.
    /// </summary>
    public bool WeatherNoticeSeen { get; set; }

    /// <summary>Fahrenheit rather than Celsius.</summary>
    public bool Fahrenheit { get; set; } = RegionInfo.CurrentRegion.Name is
        "US" or "LR" or "MM" or "BS" or "BZ" or "KY" or "PW";

    [JsonIgnore]
    public bool HasLocation => !double.IsNaN(Latitude) && !double.IsNaN(Longitude);

    public AthanMode ModeFor(string slot) =>
        Modes.TryGetValue(slot, out var mode) ? mode : AthanMode.Sound;

    public void SetMode(string slot, AthanMode mode)
    {
        Modes[slot] = mode;
        Save();
    }

    // ---- persistence -------------------------------------------------------

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Athan");

    private static readonly string File_ = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(File_), Json) ?? new Settings();
        }
        catch
        {
            // A corrupt settings file must not stop the app starting; defaults
            // are always usable and the user can set their location again.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(File_, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // Read-only profile or roaming hiccup: losing a preference is not
            // worth crashing over, and the in-memory value still applies.
        }
    }
}
