using System.Globalization;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class LocationPickerDialog : Window
{
    public double Latitude  { get; private set; }
    public double Longitude { get; private set; }

    private readonly double? _initLat;
    private readonly double? _initLon;

    public LocationPickerDialog(double? lat = null, double? lon = null)
    {
        InitializeComponent();
        _initLat = lat;
        _initLon = lon;
        if (lat.HasValue && lon.HasValue)
        {
            Latitude  = lat.Value;
            Longitude = lon.Value;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.CoreWebView2.WebMessageReceived += OnMapMessage;
            MapWebView.CoreWebView2.NavigateToString(BuildMapHtml());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 unavailable: {ex.Message}");
            // WebView2 not available — fall back to manual coordinate input
            MapWebView.Visibility  = Visibility.Collapsed;
            CoordLabel.Visibility  = Visibility.Collapsed;
            ManualPanel.Visibility = Visibility.Visible;
            if (_initLat.HasValue && _initLon.HasValue)
            {
                TxtLat.Text        = _initLat.Value.ToString("F5", CultureInfo.InvariantCulture);
                TxtLon.Text        = _initLon.Value.ToString("F5", CultureInfo.InvariantCulture);
                OkButton.IsEnabled = true;
            }
        }
    }

    private void OnMapMessage(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        var parts = e.TryGetWebMessageAsString().Split(',');
        if (parts.Length != 2) return;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;

        Latitude  = lat;
        Longitude = lon;
        Dispatcher.Invoke(() =>
        {
            CoordLabel.Text    = $"Selected: {lat:F5}°, {lon:F5}°";
            OkButton.IsEnabled = true;
        });
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        // Manual entry fallback
        if (ManualPanel.Visibility == Visibility.Visible)
        {
            if (!double.TryParse(TxtLat.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(TxtLon.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                lat < -90 || lat > 90 || lon < -180 || lon > 180)
            {
                MessageBox.Show("Enter valid latitude (−90 to 90) and longitude (−180 to 180).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Latitude  = lat;
            Longitude = lon;
        }
        DialogResult = true;
    }

    private string BuildMapHtml()
    {
        var ic   = CultureInfo.InvariantCulture;
        var lat  = (_initLat ?? 20.0).ToString("F5", ic);
        var lon  = (_initLon ?? 0.0).ToString("F5", ic);
        var zoom = _initLat.HasValue ? 10 : 2;

        var initMarker = _initLat.HasValue
            ? $"marker = L.marker([{lat},{lon}]).addTo(map);"
            : "";

        return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>html,body{{margin:0;padding:0;height:100%}}#map{{height:100%}}</style>
</head><body>
<div id='map'></div>
<script>
var map = L.map('map').setView([{lat},{lon}],{zoom});
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{
  attribution:'&copy; OpenStreetMap contributors'
}}).addTo(map);
var marker=null;
{initMarker}
map.on('click',function(e){{
  if(marker)marker.remove();
  marker=L.marker(e.latlng).addTo(map);
  window.chrome.webview.postMessage(e.latlng.lat+','+e.latlng.lng);
}});
</script></body></html>";
    }
}
