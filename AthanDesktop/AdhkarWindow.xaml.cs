using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AthanDesktop;

/// <summary>
/// The morning and evening athkar. One window for both sittings - they differ
/// only in which file is read and what the heading says, and two windows would
/// be two places for the same layout to drift.
/// </summary>
public partial class AdhkarWindow : Window
{
    public AdhkarWindow(AdhkarSitting sitting)
    {
        InitializeComponent();

        TitleText.Text = Strings.Get(sitting == AdhkarSitting.Morning ? "adhkar_morning" : "adhkar_evening");
        WhenText.Text = Strings.Get(sitting == AdhkarSitting.Morning ? "adhkar_morning_when" : "adhkar_evening_when");
        Title = TitleText.Text;

        var adhkar = Adhkar.Load(sitting);
        if (adhkar.Count == 0)
        {
            List.Items.Add(new TextBlock
            {
                Text = Strings.Get("adhkar_unavailable"),
                Foreground = (Brush)FindResource("TextDim"),
            });
            return;
        }

        for (var i = 0; i < adhkar.Count; i++) List.Items.Add(Card(adhkar[i], i + 1));
    }

    /// <summary>
    /// One card per dhikr. Nothing is ever trimmed: the words are the point of
    /// the window, so the card grows to fit them.
    /// </summary>
    private Border Card(Dhikr dhikr, int number)
    {
        var header = new DockPanel { LastChildFill = false };

        var index = new TextBlock
        {
            Text = number.ToString(),
            FontSize = 13,
            Foreground = (Brush)FindResource("TextDim"),
        };
        DockPanel.SetDock(index, Dock.Left);
        header.Children.Add(index);

        if (dhikr.Repeat.Length > 0)
        {
            var badge = new Border
            {
                Background = (Brush)FindResource("SurfaceHi"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 4),
                Child = new TextBlock
                {
                    Text = dhikr.Repeat,
                    FontSize = 12.5,
                    Foreground = (Brush)FindResource("Accent"),
                },
            };
            DockPanel.SetDock(badge, Dock.Right);
            header.Children.Add(badge);
        }

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(new TextBlock
        {
            Text = dhikr.Text,
            FontSize = 16,
            LineHeight = 30,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = (Brush)FindResource("Text"),
        });
        if (dhikr.Source.Length > 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = dhikr.Source,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = (Brush)FindResource("TextDim"),
            });
        }

        return new Border
        {
            Background = (Brush)FindResource("Surface"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 12),
            Child = body,
        };
    }
}
