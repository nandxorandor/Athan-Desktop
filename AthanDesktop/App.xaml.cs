using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AthanDesktop;

/// <summary>
/// The app itself lives here rather than in a window: it must keep running with
/// every window closed, waiting for the next prayer. Closing the main window
/// hides it to the tray; only "Quit" actually exits.
/// </summary>
public partial class App : Application
{
    public static Settings Settings { get; private set; } = new();
    public static AthanCatalog Catalog { get; private set; } = null!;
    public static PrayerEngine Engine { get; private set; } = null!;
    public static AudioPlayer Player { get; private set; } = null!;

    private Forms.NotifyIcon? _tray;
    private DispatcherTimer? _timer;
    private MainWindow? _main;
    private AthanWindow? _athanWindow;

    /// <summary>The prayer we are currently counting down to.</summary>
    private Upcoming? _next;

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Athan";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Settings = Settings.Load();
        Catalog = new AthanCatalog();
        Engine = new PrayerEngine(Settings);
        Player = new AudioPlayer();

        ApplyStartupDefault();
        SetUpTray();
        Reschedule();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();

        // Started by the Run key at login: come up in the tray, not in the
        // user's face while they are trying to get to their desktop.
        var silent = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        if (!silent) ShowMain();
        else if (!Settings.HasLocation) ShowMain();
    }

    /// <summary>
    /// Registers the startup entry once, on first run. Done here rather than on
    /// every launch so that unticking the box in Settings sticks - re-applying a
    /// default over a user's explicit choice would be worse than not having one.
    /// </summary>
    private static void ApplyStartupDefault()
    {
        if (!Settings.StartupApplied)
        {
            // First run, or a settings file written before this flag existed
            // whose stored "false" was the old default rather than a choice.
            Settings.StartupApplied = true;
            Settings.StartWithWindows = true;
            Settings.Save();
        }

        // Reconciled on every launch rather than written once: the registry
        // value carries the exe's path, so moving or renaming Athan.exe would
        // otherwise leave a startup entry pointing at nothing. The checkbox
        // stays the single source of truth.
        SetStartWithWindows(Settings.StartWithWindows);
    }

    // ---- tray --------------------------------------------------------------

    private void SetUpTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Athan", null, (_, _) => ShowMain());
        menu.Items.Add("Settings", null, (_, _) => { ShowMain(); _main?.OpenSettings(); });
        menu.Items.Add("About", null, (_, _) => new AboutWindow().ShowDialog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Stop athan", null, (_, _) => StopAthan());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => QuitApp());

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Athan",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // The icon is embedded in the exe as its application icon, so it can be
        // read back from the running file - no separate .ico to ship.
        var path = Environment.ProcessPath;
        if (path is not null)
        {
            var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (extracted is not null) return extracted;
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void UpdateTrayText()
    {
        if (_tray is null) return;
        _tray.Text = _next is null
            ? "Athan - set your location"
            // NotifyIcon tooltips are capped at 63 characters by Windows.
            : Truncate($"{_next.Slot.Label} at {_next.Time:h:mm tt}", 63);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    // ---- scheduling --------------------------------------------------------

    /// <summary>
    /// Recomputes what we are waiting for. <paramref name="guard"/> skips
    /// prayers within that window of now: straight after a firing, the prayer
    /// that just went off is still a hair in the past on some clocks, and
    /// re-selecting it would fire the athan again immediately.
    /// </summary>
    public void Reschedule(TimeSpan guard = default)
    {
        _next = Engine.Next(DateTime.Now + guard);
        UpdateTrayText();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _main?.Tick();

        if (_next is null)
        {
            // No location yet, or it was only just set - keep looking.
            if (Settings.HasLocation) Reschedule();
            return;
        }

        if (DateTime.Now >= _next.Time)
        {
            var fired = _next.Slot;
            Reschedule(TimeSpan.FromMinutes(1));
            FireAthan(fired);
        }
        else
        {
            UpdateTrayText();
        }
    }

    // ---- the athan ---------------------------------------------------------

    /// <summary>
    /// <paramref name="force"/> is the Test button: it must be audible whatever
    /// that prayer's mode happens to be, or testing a Silent prayer would look
    /// like a broken app.
    /// </summary>
    public void FireAthan(Slot slot, bool force = false)
    {
        if (!slot.Notifies) return;

        var mode = force ? AthanMode.Sound : Settings.ModeFor(slot.Name);
        // Silent means silent: no recording and no window. Popup means the
        // window without the recording - you still know the time has come.
        if (mode == AthanMode.Silent) return;
        if (mode == AthanMode.Sound) PlayFor(slot);

        _athanWindow?.Close();
        _athanWindow = new AthanWindow(slot);
        _athanWindow.Closed += (_, _) => { _athanWindow = null; StopAthan(); };
        _athanWindow.Show();
        _athanWindow.Activate();
    }

    private void PlayFor(Slot slot)
    {
        var key = slot == Slot.Fajr ? Catalog.ResolveFajr(Settings) : Catalog.ResolveGeneral(Settings);
        if (key is null) return;
        var stream = Catalog.Open(key);
        if (stream is null) return;
        Player.Play(stream, Settings.Volume);
    }

    public void StopAthan()
    {
        Player.Stop();
        if (_athanWindow is not null)
        {
            var w = _athanWindow;
            _athanWindow = null;
            w.Close();
        }
    }

    /// <summary>Preview from the sound picker, at the configured volume.</summary>
    public void Preview(string key)
    {
        var stream = Catalog.Open(key);
        if (stream is not null) Player.Play(stream, Settings.Volume);
    }

    // ---- windows -----------------------------------------------------------

    public void ShowMain()
    {
        if (_main is null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) => _main = null;
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void QuitApp()
    {
        _timer?.Stop();
        Player.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        Shutdown();
    }

    // ---- start with Windows ------------------------------------------------

    /// <summary>
    /// A Run-key entry rather than a scheduled task or a shortcut: it needs no
    /// elevation, it moves with the exe, and the user can see and remove it in
    /// Task Manager's Startup tab like any other app.
    /// </summary>
    public static void SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is null) return;
                key.SetValue(RunValue, $"\"{exe}\" --tray");
            }
            else
            {
                key.DeleteValue(RunValue, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Locked-down machine or a managed profile: not being able to add a
            // startup entry is not a reason to fail the settings screen.
        }
    }

    public static bool IsStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is not null;
        }
        catch { return false; }
    }
}
