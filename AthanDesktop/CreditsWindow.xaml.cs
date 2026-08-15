using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AthanDesktop;

/// <summary>
/// Where every bundled recording came from. Not decoration: IslamWeb permits its
/// material to be used non-commercially provided the source is named, so this
/// screen is the condition being met. Built from the shipped audio index rather
/// than a hand-written list, because a credit that can drift out of step with
/// the files is worse than none.
/// </summary>
public partial class CreditsWindow : Window
{
    private const string FatwaUrl = "https://www.islamweb.net/en/fatwa/379009/";

    public CreditsWindow()
    {
        InitializeComponent();

        foreach (var group in App.Catalog.ByCategory())
        {
            CreditsList.Children.Add(new TextBlock
            {
                Text = group.Key,
                Foreground = (Brush)FindResource("Accent"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4),
            });

            foreach (var sound in group)
            {
                // No trimming anywhere here: reciter names are long, and an
                // ellipsised credit is not a credit.
                CreditsList.Children.Add(new TextBlock
                {
                    Text = sound.Label,
                    Foreground = (Brush)FindResource("Text"),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0),
                });
                CreditsList.Children.Add(new TextBlock
                {
                    Text = CreditFor(sound),
                    Foreground = (Brush)FindResource("TextDim"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 0),
                });
            }
        }
    }

    /// <summary>
    /// The recording's own artist tag where it has one. Where it does not, say
    /// so plainly rather than inventing a source.
    /// </summary>
    private static string CreditFor(AthanSound sound)
    {
        if (sound.Category == "developer" || sound.Key.EndsWith("Developer_Athan-3_Fajr.mp3", StringComparison.Ordinal))
            return "Recorded by the developer";
        if (IsReadable(sound.Source))
            return "Source: " + Tidy(sound.Source);
        return "Source: islamweb.net (reciter not named in the recording)";
    }

    /// <summary>
    /// A few recordings carry an ID3 tag in an encoding that could not be
    /// recovered, and arrive as replacement characters. Showing a row of
    /// diamonds as someone's credit is worse than admitting the tag is missing.
    /// </summary>
    private static bool IsReadable(string source) =>
        !string.IsNullOrWhiteSpace(source) && !source.Contains('�');

    /// <summary>Tags carry things like "www.islamweb.net\&lt;Arabic name&gt;"; show one line.</summary>
    private static string Tidy(string source) =>
        System.Text.RegularExpressions.Regex.Replace(source.Replace('\\', ' '), @"\s+", " ").Trim();

    private void Fatwa_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(FatwaUrl) { UseShellExecute = true });
        }
        catch
        {
            // No default browser configured: nothing worth interrupting for.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
