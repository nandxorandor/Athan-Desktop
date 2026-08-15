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

        _loading = false;
        Refresh();
    }

    private void Refresh()
    {
        FajrSoundButton.Content = App.Catalog.LabelFor(App.Catalog.ResolveFajr(App.Settings));
        OtherSoundButton.Content = App.Catalog.LabelFor(App.Catalog.ResolveGeneral(App.Settings));
        VolumeLabel.Text = $"Volume — {App.Settings.Volume}%";
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
