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
    /// Whether the default above has been acted on. Without this the app would
    /// re-add the startup entry on every launch, silently overriding a user who
    /// deliberately turned it off.
    /// </summary>
    public bool StartupApplied { get; set; }

    /// <summary>Closing the window hides to the tray rather than quitting; shown once.</summary>
    public bool TrayNoticeSeen { get; set; }

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
