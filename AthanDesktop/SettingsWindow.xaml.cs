using System.Windows;
using System.Windows.Controls;
using Batoulapps.Adhan;

namespace AthanDesktop;

public partial class SettingsWindow : Window
{
    /// <summary>Set while filling controls, so setup does not look like user input.</summary>
    private bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();

        // Built from the enum rather than a hardcoded list, so the picker cannot
        // drift out of sync with whatever the library actually ships.
        foreach (var method in Enum.GetValues<CalculationMethod>())
            MethodBox.Items.Add(new ComboBoxItem { Content = MethodLabel(method.ToString()), Tag = method.ToString() });
        SelectByTag(MethodBox, App.Settings.Method);

        MadhabBox.Items.Add(new ComboBoxItem { Content = "Standard (Shafi'i, Maliki, Hanbali)", Tag = "SHAFI" });
        MadhabBox.Items.Add(new ComboBoxItem { Content = "Hanafi", Tag = "HANAFI" });
        SelectByTag(MadhabBox, App.Settings.Madhab);

        VolumeSlider.Value = App.Settings.Volume;
        AdjustSlider.Value = App.Settings.AdjustmentMinutes;
        StartupCheck.IsChecked = App.IsStartWithWindows();
        ShowOnStartupCheck.IsChecked = App.Settings.ShowWindowOnStartup;
        RamadanPromptCheck.IsChecked = App.Settings.RamadanPromptEnabled;
        DuaCheck.IsChecked = App.Settings.AfterAthanDuaEnabled;
        WeatherCheck.IsChecked = App.Settings.WeatherEnabled;
        UnitCelsius.IsChecked = !App.Settings.Fahrenheit;
        UnitFahrenheit.IsChecked = App.Settings.Fahrenheit;
        SyncUnitState();
        ReminderCheck.IsChecked = App.Settings.ReminderEnabled;

        foreach (var m in new[] { 5, 10, 15, 20, 30 })
            ReminderMinutesBox.Items.Add(new ComboBoxItem { Content = $"{m} minutes before", Tag = m });
        SelectMinutes(App.Settings.ReminderMinutes);

        _loading = false;
        Refresh();
    }

    private void Refresh()
    {
        FajrSoundButton.Content = App.Catalog.LabelFor(App.Catalog.ResolveFajr(App.Settings));
        OtherSoundButton.Content = App.Catalog.LabelFor(App.Catalog.ResolveGeneral(App.Settings));
        VolumeLabel.Text = $"Volume — {App.Settings.Volume}%";
        RamadanWhen.Text = RamadanSummary();
        ReminderMinutesLabel.Text = "How early";
        ReminderMinutesBox.IsEnabled = App.Settings.ReminderEnabled;
        // Nothing to decide about a window at login if there is no launch at login.
        ShowOnStartupCheck.IsEnabled = StartupCheck.IsChecked == true;
        AdjustLabel.Text = App.Settings.AdjustmentMinutes switch
        {
            0 => "Time adjustment — none",
            > 0 => $"Time adjustment — +{App.Settings.AdjustmentMinutes} min",
            _ => $"Time adjustment — −{-App.Settings.AdjustmentMinutes} min",
        };
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if ((string?)item.Tag == tag) { box.SelectedItem = item; return; }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static string? TagOf(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string;

    // ---- handlers ----------------------------------------------------------

    private void FajrSound_Click(object sender, RoutedEventArgs e) => PickSound(forFajr: true);

    private void OtherSound_Click(object sender, RoutedEventArgs e) => PickSound(forFajr: false);

    private void PickSound(bool forFajr)
    {
        var picker = new SoundPickerWindow(forFajr) { Owner = this };
        picker.ShowDialog();
        Refresh();
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        App.Settings.Volume = (int)e.NewValue;
        App.Settings.Save();
        // Live, so dragging the slider while a preview plays is audible.
        App.Player.SetVolume(App.Settings.Volume);
        Refresh();
    }

    private void Adjust_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        App.Settings.AdjustmentMinutes = (int)e.NewValue;
        App.Settings.Save();
        Refresh();
        ((App)Application.Current).Reschedule();
    }

    private void Method_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Method = TagOf(MethodBox) ?? "NORTH_AMERICA";
        App.Settings.Save();
        ((App)Application.Current).Reschedule();
    }

    private void Madhab_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Madhab = TagOf(MadhabBox) ?? "SHAFI";
        App.Settings.Save();
        ((App)Application.Current).Reschedule();
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var on = StartupCheck.IsChecked == true;
        App.SetStartWithWindows(on);
        App.Settings.StartWithWindows = on;
        App.Settings.Save();
        Refresh();
    }

    private void ShowOnStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.ShowWindowOnStartup = ShowOnStartupCheck.IsChecked == true;
        App.Settings.Save();
    }

    private void SelectMinutes(int minutes)
    {
        foreach (ComboBoxItem item in ReminderMinutesBox.Items)
        {
            if ((int)item.Tag! == minutes) { ReminderMinutesBox.SelectedItem = item; return; }
        }
        if (ReminderMinutesBox.Items.Count > 0) ReminderMinutesBox.SelectedIndex = 1;
    }

    /// <summary>
    /// Units mean nothing while the feature is off, so they are greyed out
    /// rather than letting someone set a preference that does not apply.
    /// </summary>
    private void SyncUnitState()
    {
        var on = WeatherCheck.IsChecked == true;
        UnitCelsius.IsEnabled = on;
        UnitFahrenheit.IsEnabled = on;
    }

    private void Weather_Changed(object sender, RoutedEventArgs e)
    {
        SyncUnitState();
        if (_loading) return;
        App.Settings.WeatherEnabled = WeatherCheck.IsChecked == true;
        // Answered here, so the main window never asks again.
        App.Settings.WeatherNoticeSeen = true;
        App.Settings.Save();
        if (!App.Settings.WeatherEnabled) Weather.Forget();
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Fahrenheit = UnitFahrenheit.IsChecked == true;
        App.Settings.Save();
    }

    private void Dua_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.AfterAthanDuaEnabled = DuaCheck.IsChecked == true;
        App.Settings.Save();
    }

    private void Reminder_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.ReminderEnabled = ReminderCheck.IsChecked == true;
        App.Settings.Save();
        Refresh();
    }

    private void ReminderMinutes_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if ((ReminderMinutesBox.SelectedItem as ComboBoxItem)?.Tag is not int minutes) return;
        App.Settings.ReminderMinutes = minutes;
        App.Settings.Save();
    }

    /// <summary>
    /// Seeing it once beats discovering at Fajr that it appears somewhere you
    /// were not looking, or that the countdown is too quick to read.
    /// </summary>
    private void PreviewReminder_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).PreviewReminder();

    /// <summary>Says when the next Ramadan falls, so the button is not a blind door.</summary>
    private static string RamadanSummary()
    {
        var year = RamadanCalendar.UpcomingHijriYear();
        if (year == 0) return "The Umm al-Qura calendar is unavailable on this system.";
        var first = RamadanCalendar.FirstDay(year);
        if (first is null) return "";
        var days = RamadanCalendar.DaysIn(year);
        var last = first.Value.AddDays(days - 1);
        var away = (first.Value.Date - DateTime.Today).Days;
        var when = away switch
        {
            > 1 => $"in {away} days",
            1 => "tomorrow",
            0 => "today",
            _ => "under way",
        };
        return $"Ramadan {year} AH begins {when} — {first.Value:d MMMM yyyy} to {last:d MMMM yyyy}, {days} days.";
    }

    private void Ramadan_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).ShowRamadan(RamadanCalendar.UpcomingHijriYear());

    private void RamadanPrompt_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.RamadanPromptEnabled = RamadanPromptCheck.IsChecked == true;
        // Re-enabling clears any "don't ask again", otherwise the switch would
        // appear to do nothing for the rest of the year it was dismissed in.
        if (App.Settings.RamadanPromptEnabled) App.Settings.RamadanPromptDismissedYear = 0;
        App.Settings.Save();
    }

    private void Credits_Click(object sender, RoutedEventArgs e) =>
        new CreditsWindow { Owner = this }.ShowDialog();

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        App.Player.Stop();
        Close();
    }

    private static string MethodLabel(string name) => name switch
    {
        "NORTH_AMERICA" => "North America (ISNA)",
        "MUSLIM_WORLD_LEAGUE" => "Muslim World League",
        "EGYPTIAN" => "Egyptian General Authority",
        "KARACHI" => "Karachi",
        "UMM_AL_QURA" => "Umm al-Qura (Makkah)",
        "DUBAI" => "Dubai",
        "MOON_SIGHTING_COMMITTEE" => "Moonsighting Committee",
        "KUWAIT" => "Kuwait",
        "QATAR" => "Qatar",
        "SINGAPORE" => "Singapore",
        "OTHER" => "Other",
        _ => name.Replace('_', ' '),
    };
}
