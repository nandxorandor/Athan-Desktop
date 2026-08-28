using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AthanDesktop;

/// <summary>
/// The whole of Ramadan in one table, previewed and then saved. Doubles as the
/// seasonal offer: opened from the prompt it explains itself and shows the
/// "don't ask again" box, rather than making a second window say the same thing
/// with a different button.
/// </summary>
public partial class RamadanWindow : Window
{
    private readonly bool _fromPrompt;
    private int _hijriYear;
    private List<RamadanDay> _days = new();

    public RamadanWindow(int hijriYear, bool fromPrompt = false)
    {
        InitializeComponent();
        _fromPrompt = fromPrompt;
        _hijriYear = hijriYear;

        if (fromPrompt)
        {
            Intro.Visibility = Visibility.Visible;
            Intro.Text = $"Ramadan {hijriYear} is nearly here. Would you like a prayer-time calendar " +
                         "for the whole month? It fits on one page, ready to print.";
            DontAsk.Visibility = Visibility.Visible;
        }

        // A couple of years either side, so someone can print next year's early
        // or look back at the one just gone.
        var current = RamadanCalendar.UpcomingHijriYear();
        if (current == 0) current = hijriYear;
        for (var y = current - 1; y <= current + 2; y++)
            YearBox.Items.Add(new ComboBoxItem { Content = $"Ramadan {y} AH", Tag = y });
        foreach (ComboBoxItem item in YearBox.Items)
            if ((int)item.Tag! == hijriYear) YearBox.SelectedItem = item;
        if (YearBox.SelectedItem is null && YearBox.Items.Count > 0) YearBox.SelectedIndex = 0;

        Load();
    }

    private void Year_Changed(object sender, SelectionChangedEventArgs e)
    {
        if ((YearBox.SelectedItem as ComboBoxItem)?.Tag is int year && year != _hijriYear)
        {
            _hijriYear = year;
            Load();
        }
    }

    private void Load()
    {
        Heading.Text = $"Ramadan {_hijriYear} AH";
        _days = RamadanCalendar.Build(_hijriYear, App.Settings);
        Preview.Children.Clear();

        if (!App.Settings.HasLocation)
        {
            SubHeading.Text = "Set your location first — the times are calculated from it.";
            SaveButton.IsEnabled = false;
            return;
        }
        if (_days.Count == 0)
        {
            SubHeading.Text = "That year is outside the Umm al-Qura calendar Windows provides.";
            SaveButton.IsEnabled = false;
            return;
        }

        SaveButton.IsEnabled = true;
        var place = string.IsNullOrWhiteSpace(App.Settings.CityName)
            ? $"{App.Settings.Latitude:0.##}, {App.Settings.Longitude:0.##}"
            : App.Settings.CityName;
        SubHeading.Text = $"{place}  ·  {_days[0].Date:d MMMM yyyy} – {_days[^1].Date:d MMMM yyyy}" +
                          $"  ·  {_days.Count} days";

        Preview.Children.Add(BuildRow(
            new[] { "Ramadan", "Date", "Suhoor ends", "Sunrise", "Dhuhr", "Asr", "Iftar", "Isha" },
            heading: true, friday: false, today: false));
        foreach (var d in _days)
        {
            Preview.Children.Add(BuildRow(new[]
            {
                d.DayOfRamadan.ToString(),
                d.Date.ToString("ddd, d MMM"),
                Time(d.Fajr), Time(d.Sunrise), Time(d.Dhuhr),
                Time(d.Asr), Time(d.Maghrib), Time(d.Isha),
            }, heading: false,
               friday: d.Date.DayOfWeek == DayOfWeek.Friday,
               today: d.Date.Date == DateTime.Today));
        }
    }

    private static string Time(DateTime t) => t.ToString("h:mm tt", CultureInfo.CurrentCulture);

    /// <summary>Same column weights as the document, so the preview is honest.</summary>
    private static readonly int[] Weights = { 8, 15, 16, 12, 12, 12, 16, 12 };

    private UIElement BuildRow(IReadOnlyList<string> cells, bool heading, bool friday, bool today = false)
    {
        var grid = new Grid();
        foreach (var w in Weights)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

        for (var i = 0; i < cells.Count; i++)
        {
            // Suhoor and iftar are the two the fast actually turns on.
            var key = i is 2 or 6;
            var text = new TextBlock
            {
                Text = cells[i],
                FontSize = heading ? 11.5 : 12.5,
                FontWeight = heading || key ? FontWeights.SemiBold : FontWeights.Normal,
                // On the highlighted row the dim greys and the accent both
                // lose their contrast, so everything in it goes to one colour.
                Foreground = heading
                    ? (Brush)FindResource("TextDim")
                    : today ? (Brush)FindResource("TodayText")
                    : key ? (Brush)FindResource("Accent") : (Brush)FindResource("Text"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }

        return new Border
        {
            Child = grid,
            // Today first, then Fridays. During the month the row you want is
            // today's, and it has to be findable without reading a date.
            Background = heading ? Brushes.Transparent
                : today ? (Brush)FindResource("TodayRow")
                : friday ? (Brush)FindResource("FridayRow")
                : Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, heading ? 8 : 6, 10, heading ? 8 : 6),
            Margin = new Thickness(0, 0, 0, 1),
        };
    }

    // ---- saving ------------------------------------------------------------

    private void Docx_Click(object sender, RoutedEventArgs e)
    {
        var path = AskWhereToSave("Word document (*.docx)|*.docx", "docx");
        if (path is null) return;
        try
        {
            DocxWriter.Write(path, RamadanCalendar.BuildDocx(_hijriYear, _days, App.Settings));
            Saved(path);
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    private void Csv_Click(object sender, RoutedEventArgs e)
    {
        var path = AskWhereToSave("Spreadsheet (*.csv)|*.csv", "csv");
        if (path is null) return;
        try
        {
            RamadanCalendar.WriteCsv(path, RamadanCalendar.BuildCsv(_days));
            Saved(path);
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    private string? AskWhereToSave(string filter, string extension)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Ramadan calendar",
            Filter = filter,
            DefaultExt = extension,
            FileName = $"Ramadan-{_hijriYear}-prayer-times.{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Offers to open it, because the next thing anyone wants after saving a
    /// timetable is to look at it or print it.
    /// </summary>
    private void Saved(string path)
    {
        RememberDismissal();
        var answer = MessageBox.Show(
            $"Saved to:\n{path}\n\nOpen it now?",
            "Ramadan calendar", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { /* nothing associated with .docx on this machine */ }
        }
        Close();
    }

    private void Failed(Exception ex) =>
        MessageBox.Show(
            "Couldn't save the calendar.\n\n" + ex.Message +
            "\n\nIf the file is already open in Word, close it and try again.",
            "Ramadan calendar", MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>
    /// Recorded whether they saved or declined: in both cases they have given
    /// the offer an answer for this year.
    /// </summary>
    private void RememberDismissal()
    {
        if (!_fromPrompt || DontAsk.IsChecked != true) return;
        App.Settings.RamadanPromptDismissedYear = _hijriYear;
        App.Settings.Save();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        RememberDismissal();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        RememberDismissal();
        base.OnClosing(e);
    }
}
