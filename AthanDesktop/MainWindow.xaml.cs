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
        Refresh();
        if (!App.Settings.HasLocation) Dispatcher.BeginInvoke(ChooseLocation);
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
    }

    public void Refresh()
    {
        CityName.Text = App.Settings.HasLocation
            ? (string.IsNullOrWhiteSpace(App.Settings.CityName) ? "Location set" : App.Settings.CityName)
            : "Set your location";

        HijriDate.Text = HijriToday();

        TimesList.Items.Clear();
        var times = App.Engine.Today();
        if (times.Count == 0)
        {
            NextLabel.Visibility = Visibility.Collapsed;
            NextPrayer.Text = "No location yet";
            NextPrayer.FontSize = 22;
            NextAt.Text = "Click the name above to set one";
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
            : $"Qibla {Math.Round(bearing.Value)}° from true north  ·  {Math.Round(distance ?? 0):N0} km to Mecca";

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
            Text = slot.Notifies ? slot.Label : "☀  " + slot.Label,
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
            Text = time.ToString("h:mm tt", CultureInfo.CurrentCulture),
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
            AthanMode.Sound => ("", "Sound", (Brush)FindResource("Accent")),
            AthanMode.Popup => ("", "Popup", (Brush)FindResource("Sunrise")),
            _ => ("", "Silent", (Brush)FindResource("IconIdle")),
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
            ? $"{(int)left.TotalHours}h {left.Minutes:00}m"
            : $"{left.Minutes}m {left.Seconds:00}s";
        NextPrayer.Text = $"{next.Slot.Label} in {text}";
        NextAt.Text = $"at {next.Time:h:mm tt}";
    }

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

    private void Credits_Click(object sender, RoutedEventArgs e) =>
        new CreditsWindow { Owner = this }.ShowDialog();

    /// <summary>
    /// Hearing it now beats finding out at Fajr that the volume was wrong or no
    /// sound device was selected. Always plays, whatever Dhuhr's mode is set to.
    /// </summary>
    private void Test_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).FireAthan(Slot.Dhuhr, force: true);
}
