using System.Windows;

namespace AthanDesktop;

/// <summary>
/// The one surface that says a prayer has come in, and the only place with a
/// Stop button. Deliberately not a tray balloon: at Fajr the point is to be
/// impossible to miss.
/// </summary>
public partial class AthanWindow : Window
{
    public AthanWindow(Slot slot)
    {
        InitializeComponent();
        PrayerName.Text = slot.Label;
        ClockText.Text = DateTime.Now.ToString("h:mm tt");

        // Silent mode still gets the window - the user asked not to hear it, not
        // to be left unaware that the time has come.
        Loaded += (_, _) =>
        {
            Activate();
            Topmost = true;
        };
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => Close();
}
