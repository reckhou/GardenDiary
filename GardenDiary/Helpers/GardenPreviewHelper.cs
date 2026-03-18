using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GardenDiary.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace GardenDiary.Helpers;

public static class GardenPreviewHelper
{
    private static readonly string[] PlantPalette =
    [
        "#388E3C",  // green 700
        "#1565C0",  // blue 800
        "#E65100",  // deep-orange 900
        "#880E4F",  // pink 900
        "#4A148C",  // purple 900
        "#00695C",  // teal 800
        "#558B2F",  // light-green 800  (replaces lime)
        "#4E342E",  // brown 700
        "#37474F",  // blue-grey 700
        "#283593",  // indigo 800
    ];

    /// <summary>
    /// Builds a Guid→colorHex map for all plants in one O(n) pass.
    /// Colors are stable: same plant ID and emoji always map to the same palette slot
    /// regardless of list ordering.
    /// </summary>
    public static Dictionary<Guid, string> BuildColorMap(IList<Plant> plantList)
    {
        var map = new Dictionary<Guid, string>(plantList.Count);
        foreach (var p in plantList)
            map[p.Id] = PlantPalette[PlantColorIndex(p)];
        return map;
    }

    /// <summary>
    /// Returns the palette color for a single plant. Stable — unaffected by list order.
    /// The <paramref name="plantList"/> parameter is kept for API compatibility but unused.
    /// </summary>
    public static string GetPlantColor(Plant plant, IList<Plant> plantList)
        => PlantPalette[PlantColorIndex(plant)];

    /// <summary>
    /// Deterministic palette index based on plant identity:
    /// plants sharing an emoji get the same color; emoji-less plants use their ID hash.
    /// </summary>
    private static int PlantColorIndex(Plant plant)
    {
        int hash;
        if (string.IsNullOrWhiteSpace(plant.Emoji))
        {
            hash = Math.Abs(plant.Id.GetHashCode());
        }
        else
        {
            // string.GetHashCode() is randomised per-process in .NET Core+,
            // so sum the Unicode code points instead for a stable result.
            hash = 0;
            foreach (var rune in plant.Emoji.EnumerateRunes())
                hash += rune.Value;
        }
        return hash % PlantPalette.Length;
    }

    /// <summary>
    /// Draws a scaled mini-map into <paramref name="canvas"/>, highlighting
    /// <paramref name="targetPlant"/> in black (highlighted=true) or red (highlighted=false).
    /// </summary>
    public static void Draw(Canvas canvas, Plant targetPlant, GardenArea area,
                            IList<Plant> allPlants, bool highlighted = true)
    {
        canvas.Children.Clear();

        var cw = canvas.ActualWidth;
        var ch = canvas.ActualHeight;
        if (cw <= 0 || ch <= 0) return;

        if (area.Width <= 0 || area.Height <= 0) return;

        const double margin = 6;
        var scale = Math.Min((cw - margin * 2) / area.Width, (ch - margin * 2) / area.Height);
        var ox    = (cw - area.Width  * scale) / 2;
        var oy    = (ch - area.Height * scale) / 2;

        // Area outline
        var areaRect = new Rectangle
        {
            Width            = area.Width  * scale,
            Height           = area.Height * scale,
            Stroke           = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            StrokeThickness  = 1,
            Fill             = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(areaRect, ox);
        Canvas.SetTop(areaRect,  oy);
        canvas.Children.Add(areaRect);

        // Shapes (faint)
        foreach (var shape in area.Shapes.OrderBy(s => s.ZIndex))
        {
            var fc = (Color)ColorConverter.ConvertFromString(shape.FillColor);
            FrameworkElement sv = shape.Type == ShapeType.Circle
                ? new Ellipse   { Width = shape.Width * scale, Height = shape.Height * scale,
                                  Fill = new SolidColorBrush(fc) { Opacity = 0.35 } }
                : new Rectangle { Width = shape.Width * scale, Height = shape.Height * scale,
                                  Fill = new SolidColorBrush(fc) { Opacity = 0.35 } };
            sv.IsHitTestVisible = false;
            Canvas.SetLeft(sv, ox + shape.X * scale);
            Canvas.SetTop(sv,  oy + shape.Y * scale);
            canvas.Children.Add(sv);
        }

        // Plant circles
        var colorMap    = BuildColorMap(allPlants);
        var targetBrush = highlighted ? Brushes.Black : Brushes.Red;
        foreach (var placement in area.PlantPlacements)
        {
            var p = allPlants.FirstOrDefault(pl => pl.Id == placement.PlantId);
            if (p == null) continue;

            var isTarget = p.Id == targetPlant.Id;
            var colorHex = colorMap.TryGetValue(p.Id, out var c) ? c : "#66BB6A";
            var col      = (Color)ColorConverter.ConvertFromString(colorHex);
            var r        = Math.Max(placement.Radius * scale, isTarget ? 5 : 3);
            var cx       = ox + placement.X * scale;
            var cy       = oy + placement.Y * scale;

            var ellipse = new Ellipse
            {
                Width            = r * 2,
                Height           = r * 2,
                Fill             = new SolidColorBrush(col) { Opacity = isTarget ? 0.9 : 0.45 },
                Stroke           = isTarget ? targetBrush : Brushes.Transparent,
                StrokeThickness  = isTarget ? 1.5 : 0,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ellipse, cx - r);
            Canvas.SetTop(ellipse,  cy - r);
            canvas.Children.Add(ellipse);

            if (isTarget)
            {
                var lbl = new TextBlock
                {
                    Text             = p.CommonName,
                    FontSize         = 9,
                    Foreground       = targetBrush,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(lbl, cx + r + 2);
                Canvas.SetTop(lbl,  cy - 6);
                canvas.Children.Add(lbl);
            }
        }
    }
}
