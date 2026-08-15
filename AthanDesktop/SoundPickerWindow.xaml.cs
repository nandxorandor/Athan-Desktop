using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AthanDesktop;

public partial class SoundPickerWindow : Window
{
    private readonly bool _forFajr;
    private bool _loading = true;

    public SoundPickerWindow(bool forFajr)
    {
        InitializeComponent();
        _forFajr = forFajr;

        Title = forFajr ? "Fajr athan sound" : "Athan sound (other prayers)";
        Explainer.Text = forFajr
            ? "The dawn athan carries an extra line, so Fajr is chosen separately. Click a recording to hear it."
            : "Used for Dhuhr, Asr, Maghrib and Isha. Click a recording to hear it.";

        var sounds = forFajr ? App.Catalog.Fajr : App.Catalog.General;
        var current = forFajr ? App.Catalog.ResolveFajr(App.Settings) : App.Catalog.ResolveGeneral(App.Settings);

        string? lastCategory = null;
        foreach (var sound in sounds)
        {
            if (sound.CategoryLabel != lastCategory)
            {
                lastCategory = sound.CategoryLabel;
                SoundList.Items.Add(new ListBoxItem
                {
                    Content = sound.CategoryLabel.ToUpperInvariant(),
                    IsEnabled = false,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextDim"),
                    FontSize = 11,
                });
            }

            var item = new ListBoxItem
            {
                Content = string.IsNullOrEmpty(sound.Duration) ? sound.Label : $"{sound.Label}   ({sound.Duration})",
                Tag = sound.Key,
                Padding = new Thickness(6, 5, 6, 5),
            };
            SoundList.Items.Add(item);
            if (sound.Key == current) SoundList.SelectedItem = item;
        }

        // A file the user picked earlier is not in the bundled list, so give it
        // a row of its own rather than silently showing nothing as selected.
        if (current is not null && Path.IsPathRooted(current))
        {
            var item = new ListBoxItem
            {
                Content = Path.GetFileNameWithoutExtension(current) + "   (your file)",
                Tag = current,
                Padding = new Thickness(6, 5, 6, 5),
            };
            SoundList.Items.Insert(0, item);
            SoundList.SelectedItem = item;
        }

        _loading = false;
    }

    private void Sound_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if ((SoundList.SelectedItem as ListBoxItem)?.Tag is not string key) return;
        Store(key);
        ((App)Application.Current).Preview(key);
    }

    private void Store(string key)
    {
        if (_forFajr) App.Settings.FajrSound = key;
        else App.Settings.OtherSound = key;
        App.Settings.Save();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an athan recording",
            Filter = "MP3 audio (*.mp3)|*.mp3",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        // The path is stored, not a copy of the file: on a PC the user owns the
        // file and can be expected to keep it. If it disappears, the catalogue
        // falls back to a bundled recording rather than playing nothing.
        Store(dialog.FileName);
        var item = new ListBoxItem
        {
            Content = Path.GetFileNameWithoutExtension(dialog.FileName) + "   (your file)",
            Tag = dialog.FileName,
            Padding = new Thickness(6, 5, 6, 5),
        };
        SoundList.Items.Insert(0, item);
        SoundList.SelectedItem = item;
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => App.Player.Stop();

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        App.Player.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        App.Player.Stop();
        base.OnClosed(e);
    }
}
