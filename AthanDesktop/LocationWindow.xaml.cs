using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Geolocation;

namespace AthanDesktop;

public partial class LocationWindow : Window
{
    public LocationWindow()
    {
        InitializeComponent();
        SearchBox.Text = App.Settings.CityName;
        if (App.Settings.HasLocation)
        {
            LatBox.Text = App.Settings.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
            LonBox.Text = App.Settings.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        }
        ShowResults();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    // ---- detect ------------------------------------------------------------

    /// <summary>
    /// Asks Windows where this PC is, which works anywhere on earth - a bundled
    /// city list never can. The result is written into the coordinate boxes
    /// rather than saved outright, so the user sees what was found and can
    /// correct it before committing.
    /// </summary>
    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        DetectButton.IsEnabled = false;
        DetectStatus.Text = "Asking Windows…";
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                DetectStatus.Text =
                    "Windows would not share your location. Turn it on in Settings → Privacy & security → " +
                    "Location, and allow desktop apps, then try again — or pick a city or type coordinates below.";
                return;
            }

            // City-level accuracy is all prayer times need, and asking for less
            // precision lets Windows answer from a cached fix rather than
            // waking a GPS radio.
            var locator = new Geolocator { DesiredAccuracyInMeters = 3000 };
            var position = await locator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(30), timeout: TimeSpan.FromSeconds(20));

            var point = position.Coordinate.Point.Position;
            LatBox.Text = point.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
            LonBox.Text = point.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
            Results.SelectedItem = null;

            var nearest = NearestCity(point.Latitude, point.Longitude);
            DetectStatus.Text = nearest is null
                ? $"Found {point.Latitude:0.###}, {point.Longitude:0.###}. Press Save to use it."
                : $"Found {point.Latitude:0.###}, {point.Longitude:0.###} — near {nearest.Display}. Press Save to use it.";
        }
        catch (Exception ex)
        {
            DetectStatus.Text = "Couldn't get a location from Windows (" + ex.GetType().Name +
                                "). Pick a city or type coordinates below.";
        }
        finally
        {
            DetectButton.IsEnabled = true;
        }
    }

    /// <summary>Only for naming what was found; the coordinates are what get used.</summary>
    private static City? NearestCity(double lat, double lon)
    {
        City? best = null;
        var bestDistance = double.MaxValue;
        foreach (var city in Cities.All)
        {
            var d = Math.Pow(city.Latitude - lat, 2) + Math.Pow(city.Longitude - lon, 2);
            if (d < bestDistance) { bestDistance = d; best = city; }
        }
        // Roughly 1.5 degrees; beyond that "near X" would be misleading.
        return bestDistance < 2.25 ? best : null;
    }

    // ---- city list ---------------------------------------------------------

    private void Search_Changed(object sender, TextChangedEventArgs e) => ShowResults();

    private void ShowResults()
    {
        Results.Items.Clear();
        foreach (var city in Cities.Search(SearchBox.Text))
            Results.Items.Add(new ListBoxItem { Content = city.Display, Tag = city });
        if (Results.Items.Count == 0)
            Results.Items.Add(new ListBoxItem
            {
                Content = "No match — use Detect, or type coordinates below",
                IsEnabled = false,
            });
    }

    private void Results_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Selected() is not null) Save_Click(sender, e);
    }

    private City? Selected() => (Results.SelectedItem as ListBoxItem)?.Tag as City;

    // ---- save --------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var city = Selected();
        if (city is not null)
        {
            App.Settings.Latitude = city.Latitude;
            App.Settings.Longitude = city.Longitude;
            App.Settings.CityName = city.Display;
        }
        else if (TryCoordinates(out var lat, out var lon))
        {
            App.Settings.Latitude = lat;
            App.Settings.Longitude = lon;
            // Name it after the nearest known city so the main screen reads as a
            // place rather than a pair of numbers; fall back to the numbers.
            var nearest = NearestCity(lat, lon);
            App.Settings.CityName = nearest?.Display ?? $"{lat:0.##}, {lon:0.##}";
        }
        else
        {
            MessageBox.Show(
                "Press Detect my location, pick a city from the list, or type a latitude and longitude.\n\n" +
                "Latitude runs -90 to 90, longitude -180 to 180.",
                "Set location", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        App.Settings.Save();
        DialogResult = true;
        Close();
    }

    private bool TryCoordinates(out double lat, out double lon)
    {
        lat = lon = 0;
        // InvariantCulture first so a pasted "38.26" works on a machine whose
        // locale uses a comma; then the local format for typed input.
        return (double.TryParse(LatBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) ||
                double.TryParse(LatBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out lat))
               && (double.TryParse(LonBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lon) ||
                   double.TryParse(LonBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out lon))
               && lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
