using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace AthanDesktop;

/// <summary>App identity, how to reach the author, and what the app does not do.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // The informational version carries any suffix the build set; fall back
        // to the plain assembly version so this never shows blank.
        var version = Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                      ?? "1.0";
        // Strip the build metadata NuGet appends after a "+".
        var plus = version.IndexOf('+');
        if (plus > 0) version = version[..plus];
        VersionText.Text = $"Version {version} for Windows";
    }

    private void Email_Click(object sender, MouseButtonEventArgs e) =>
        Open("mailto:ahmedkhalaf1@yahoo.com");

    private void Github_Click(object sender, MouseButtonEventArgs e) =>
        Open("https://github.com/nandxorandor");

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // No mail client or browser configured: not worth interrupting for.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
