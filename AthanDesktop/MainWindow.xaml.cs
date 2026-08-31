using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AthanDesktop;

public partial class MainWindow : Window
{
    private static readonly UmAlQuraCalendar Hijri = new();

    public MainWindow()
    {
        InitializeComponent();
        ApplyLanguage();
        Refresh();
        if (!App.Settings.HasLocation) Dispatcher.BeginInvoke(ChooseLocation);
        // The temperature notice is answered before anything is fetched. The
        // Ramadan offer is the App's, not this window's - it has the guards
        // that keep it from interrupting an athan.
        Dispatcher.BeginInvoke(() => AskAboutTemperature());
    }

    /// <summary>
    /// Closing hides rather than quits - the app has to stay alive to raise the
    /// athan. Said once, because a window that ignores the close button without
    /// explanation reads as a bug.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        if (!App.Settings.TrayNoticeSeen)
        {
            App.Settings.TrayNoticeSeen = true;
            App.Settings.Save();
            MessageBox.Show(
                "Athan is still running in the notification area so it can call the prayers.\n\n" +
                "Double-click its icon to open this window again, or right-click it and choose Quit to close it properly.",
                "Athan is still running", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>Called every second by the app's timer while this window exists.</summary>
    public void Tick()
    {
        if (!IsVisible) return;
        UpdateCountdown();

        // The weather used to be fetched only by Refresh(), which runs on a
        // prayer boundary - so a window left open through a quiet afternoon
        // outlived its half-hour cache and showed nothing until the next prayer
        // or a restart. Asking every minute costs nothing: Weather.Refresh
        // returns immediately while the cached reading is still fresh, so a
        // request actually leaves the machine only once the cache has expired.
        if (DateTime.UtcNow - _lastWeatherPoll < WeatherPollEvery) return;
        _lastWeatherPoll = DateTime.UtcNow;
        Weather.Refresh(App.Settings, _ => Dispatcher.Invoke(ShowTemperature));
    }

    private DateTime _lastWeatherPoll = DateTime.MinValue;

    /// <summary>How often to consider refetching; the cache decides the rest.</summary>
    private static readonly TimeSpan WeatherPollEvery = TimeSpan.FromMinutes(1);

    public void Refresh()
    {
        CityName.Text = App.Settings.HasLocation
            ? (string.IsNullOrWhiteSpace(App.Settings.CityName)
                ? Strings.Get("location_set")
                : Ltr(App.Settings.CityName))
            : Strings.Get("set_location");

        HijriDate.Text = HijriToday();
        ShowTemperature();
        Weather.Refresh(App.Settings, _ => Dispatcher.Invoke(ShowTemperature));

        TimesList.Items.Clear();
        var times = App.Engine.Today();
        if (times.Count == 0)
        {
            NextLabel.Visibility = Visibility.Collapsed;
            NextPrayer.Text = Strings.Get("no_location_yet");
            NextPrayer.FontSize = 22;
            NextAt.Text = Strings.Get("set_location");
            QiblaLine.Text = "";
            return;
        }

        NextLabel.Visibility = Visibility.Visible;
        NextPrayer.FontSize = 34;

        var next = App.Engine.Next();
        foreach (var (slot, time) in times)
            TimesList.Items.Add(BuildRow(slot, time, isNext: next?.Slot == slot));

        var bearing = App.Engine.QiblaBearing();
        var distance = App.Engine.MeccaDistanceKm();
        QiblaLine.Text = bearing is null
            ? ""
            : Strings.Get("qibla_line",
                Ltr(Math.Round(bearing.Value).ToString(CultureInfo.InvariantCulture)),
                Ltr(Math.Round(distance ?? 0).ToString("N0", CultureInfo.InvariantCulture)));

        UpdateCountdown();
    }

    private UIElement BuildRow(Slot slot, DateTime time, bool isNext)
    {
        // Sunrise gets its own warm colour: it is a marker, not something that
        // will call you, so it must not read as one of the five.
        var brush = isNext ? (Brush)FindResource("Accent")
            : !slot.Notifies ? (Brush)FindResource("Sunrise")
            : (Brush)FindResource("Text");

        var row = new Border
        {
            // The next prayer is lifted out of the list rather than merely
            // recoloured, so a glance finds it without reading.
            Background = isNext ? (Brush)FindResource("SurfaceHi") : Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 9, 10, 9),
            Margin = new Thickness(0, 1, 0, 1),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });

        var label = new TextBlock
        {
            Text = slot.Label,
            Foreground = brush,
            FontSize = 16,
            FontWeight = isNext ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        // Sunrise has no toggle at all: offering one would imply it could call.
        if (slot.Notifies)
        {
            var chip = BuildModeChip(slot);
            Grid.SetColumn(chip, 1);
            grid.Children.Add(chip);
        }

        var clock = new TextBlock
        {
            Text = Clock(time),
            Foreground = brush,
            FontSize = 16,
            FontWeight = isNext ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(clock, 2);
        grid.Children.Add(clock);

        row.Child = grid;
        return row;
    }

    /// <summary>
    /// One tap cycles Sound to Popup to Silent. Three states on one control
    /// because the row has no room for three, and the label says which it is -
    /// an icon alone would leave "popup" indistinguishable from "silent".
    /// </summary>
    private UIElement BuildModeChip(Slot slot)
    {
        var mode = App.Settings.ModeFor(slot.Name);
        var (glyph, text, brush) = mode switch
        {
            AthanMode.Sound => ("", Strings.Get("mode_sound"), (Brush)FindResource("Accent")),
            AthanMode.Popup => ("", Strings.Get("mode_popup"), (Brush)FindResource("Sunrise")),
            _ => ("", Strings.Get("mode_silent"), (Brush)FindResource("IconIdle")),
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = brush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = brush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var chip = new Border
        {
            Child = panel,
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(11, 4, 12, 4),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand,
            ToolTip = "Click to change: Sound → Popup only → Silent",
        };
        chip.MouseLeftButtonUp += (_, _) =>
        {
            App.Settings.SetMode(slot.Name, mode switch
            {
                AthanMode.Sound => AthanMode.Popup,
                AthanMode.Popup => AthanMode.Silent,
                _ => AthanMode.Sound,
            });
            Refresh();
        };
        return chip;
    }

    private void UpdateCountdown()
    {
        var next = App.Engine.Next();
        if (next is null) return;
        var left = next.Time - DateTime.Now;
        if (left <= TimeSpan.Zero) { Refresh(); return; }

        var text = left.TotalHours >= 1
            ? Strings.Get("countdown_hm", (int)left.TotalHours, left.Minutes)
            : Strings.Get("countdown_ms", left.Minutes, left.Seconds);
        NextPrayer.Text = Strings.Get("next_in", next.Slot.Label, text);
        NextAt.Text = Strings.Get("at_time", Clock(next.Time));
    }

    /// <summary>
    /// A clock time, fenced against bidi reordering. Latin digits inside an
    /// Arabic (right-to-left) line get their parts reversed - "1:09 PM" comes
    /// out as "PM 1:09" - so the run is wrapped in left-to-right marks.
    /// </summary>
    private static string Clock(DateTime t) =>
        Ltr(t.ToString("h:mm tt", CultureInfo.InvariantCulture));

    /// <summary>Fences a Latin run so RTL layout cannot reorder it.</summary>
    public static string Ltr(string s) =>
        Strings.IsArabic ? Lrm + s + Lrm : s;

    /// <summary>U+200E. Written as an escape because it is invisible in source.</summary>
    private const string Lrm = "‎";

    /// <summary>Windows ships the Umm al-Qura calendar, so this stays offline.</summary>
    private static string HijriToday()
    {
        try
        {
            var now = DateTime.Now;
            string[] months =
            {
                "Muharram", "Safar", "Rabiʻ I", "Rabiʻ II", "Jumada I", "Jumada II",
                "Rajab", "Shaʻban", "Ramadan", "Shawwal", "Dhu al-Qaʻdah", "Dhu al-Hijjah",
            };
            return $"{now:dddd}, {Hijri.GetDayOfMonth(now)} {months[Hijri.GetMonth(now) - 1]} {Hijri.GetYear(now)} AH";
        }
        catch
        {
            return DateTime.Now.ToString("dddd, d MMMM yyyy");
        }
    }

    // ---- actions -----------------------------------------------------------

    private void Location_Click(object sender, MouseButtonEventArgs e) => ChooseLocation();

    private void ChooseLocation()
    {
        var dialog = new LocationWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Refresh();
            ((App)Application.Current).Reschedule();
        }
    }

    public void OpenSettings()
    {
        new SettingsWindow { Owner = IsVisible ? this : null }.ShowDialog();
        Refresh();
        ((App)Application.Current).Reschedule();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>
    /// Hearing it now beats finding out at Fajr that the volume was wrong or no
    /// sound device was selected. Always plays, whatever Dhuhr's mode is set to.
    /// </summary>
    private void Test_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).FireAthan(Slot.Dhuhr, force: true);

    // Owned only when this window is actually on screen. The app lives in the
    // tray, so "this" can be hidden or parked off-screen - and an owned window
    // centred on it opens where nobody can see it.
    private void ShowAdhkar(AdhkarSitting sitting) =>
        new AdhkarWindow(sitting) { Owner = IsVisible ? this : null }.ShowDialog();

    private void Morning_Click(object sender, RoutedEventArgs e) =>
        ShowAdhkar(AdhkarSitting.Morning);

    private void Evening_Click(object sender, RoutedEventArgs e) =>
        ShowAdhkar(AdhkarSitting.Evening);

    private void Ramadan_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).ShowRamadan(RamadanCalendar.UpcomingHijriYear());

    /// <summary>
    /// One button rather than a two-state switch: on a window this size a pill
    /// would cost a row, and there are only ever two languages to move between.
    /// </summary>
    private void Language_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Language = Strings.IsArabic ? "en" : "ar";
        App.Settings.Save();
        ApplyLanguage();
        Refresh();
    }

    /// <summary>
    /// Re-labels everything and flips the window's direction. WPF has no
    /// equivalent of recreating an activity, so the labels are set from code
    /// rather than bound in XAML - which also keeps them in one place.
    /// </summary>
    public void ApplyLanguage()
    {
        FlowDirection = Strings.Flow;
        SettingsButton.Content = Strings.Get("settings");
        TestButton.Content = Strings.Get("test_athan");
        MorningButton.Content = Strings.Get("adhkar_morning");
        EveningButton.Content = Strings.Get("adhkar_evening");
        RamadanButton.Content = Strings.Get("ramadan_short");
        NextLabel.Text = Strings.Get("next_prayer");
        // The button offers the other language, so it is always readable by
        // someone who cannot read the one currently on screen.
        LanguageButton.Content = Strings.IsArabic
            ? Strings.Get("lang_english")
            : Strings.Get("lang_arabic");
    }

    /// <summary>
    /// Asked once, before a single coordinate leaves this PC. Returns true if
    /// the question was put, so the caller leaves the user alone this time.
    /// </summary>
    public bool AskAboutTemperature()
    {
        if (App.Settings.WeatherNoticeSeen || !App.Settings.HasLocation) return false;

        var answer = MessageBox.Show(
            Strings.Get("temperature_notice_body"),
            Strings.Get("temperature_notice_title"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        App.Settings.WeatherNoticeSeen = true;
        App.Settings.WeatherEnabled = answer == MessageBoxResult.Yes;
        App.Settings.Save();
        if (!App.Settings.WeatherEnabled) Weather.Forget();
        Refresh();
        return true;
    }

    private void ShowTemperature()
    {
        // Last, not Current: a stale reading still beats an empty gap, and the
        // refresh below replaces it as soon as a new one arrives.
        var reading = Weather.Last;
        if (reading is null || !App.Settings.WeatherEnabled)
        {
            TemperatureBox.Visibility = Visibility.Collapsed;
            return;
        }
        // The symbol leads: the sky is what you take in at a glance, and the
        // number is what you read second. The degrees are fenced against bidi
        // reordering; the symbol is neutral either way.
        var degrees = Ltr(Weather.Format(reading, App.Settings.Fahrenheit));
        Temperature.Text = reading.Symbol.Length == 0 ? degrees : reading.Symbol + " " + degrees;
        TemperatureBox.Visibility = Visibility.Visible;
    }

    private void Temperature_Click(object sender, RoutedEventArgs e)
    {
        // The reading sits inside the location row, whose own click opens the
        // location dialog. A Button's Click does not bubble as a mouse event,
        // so the dialog no longer lands on top of the unit you just switched.
        e.Handled = true;
        App.Settings.Fahrenheit = !App.Settings.Fahrenheit;
        App.Settings.Save();
        ShowTemperature();
    }
}
