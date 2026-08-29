using System.Windows;
using System.Windows.Threading;

namespace AthanDesktop;

/// <summary>
/// The "be ready" popup, N minutes before a prayer. Unlike the athan window it
/// closes itself: this is a nudge, not a call, and one left sitting on screen
/// would still be there when the athan it warned about arrives. The button
/// counts down so the disappearance is expected rather than startling.
/// </summary>
public partial class ReminderWindow : Window
{
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _secondsLeft = VisibleSeconds;

    /// <summary>Long enough to read and register, short enough not to nag.</summary>
    private const int VisibleSeconds = 10;

    public ReminderWindow(Slot slot, int minutes)
    {
        InitializeComponent();

        Message.Text = $"{slot.Label} in {minutes} minute{(minutes == 1 ? "" : "s")}";
        AtText.Text = DateTime.Now.AddMinutes(minutes).ToString("h:mm tt");
        UpdateButton();

        _countdown.Tick += (_, _) =>
        {
            _secondsLeft--;
            if (_secondsLeft <= 0) Close();
            else UpdateButton();
        };
        _countdown.Start();

        // Off unless the user asked for it; see Settings.ReminderSoundEnabled.
        ReminderSound.Play(App.Settings);
    }

    private void UpdateButton() => CloseButton.Content = $"Close  ({_secondsLeft})";

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _countdown.Stop();
        // The window closes itself after ten seconds; a longer recording must
        // not carry on playing to an empty desktop.
        ReminderSound.Stop();
        base.OnClosed(e);
    }
}
