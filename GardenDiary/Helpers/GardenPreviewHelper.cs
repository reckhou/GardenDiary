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
        "#66BB6A", "#42A5F5", "#FFA726", "#EC407A",
        "#AB47BC", "#26A69A", "#D4E157", "#FF7043",
        "#78909C", "#8D6E63"
    ];

    /// <summary>
    /// Builds a Guid→colorHex map for all plants in one O(n) pass.
    /// Use this when coloring multiple placements to avoid O(n) per placement.
    /// </summary>
    public static Dictionary<Guid, string> BuildColorMap(IList<Plant> plantList)
    {
        var distinctEmojis = plantList
            .Where(p => !string.IsNullOrWhiteSpace(p.Emoji))
            .Select(p => p.Emoji!)
            .Distinct()
            .ToList();

        var map = new Dictionary<Guid, string>(plantList.Count);
        for (int i = 0; i < plantList.Count; i++)
        {
            var p = plantList[i];
            if (!string.IsNullOrWhiteSpace(p.Emoji))
            {
                var idx = distinctEmojis.IndexOf(p.Emoji);
                map[p.Id] = PlantPalette[idx % PlantPalette.Length];
            }
            else
            {
                map[p.Id] = PlantPalette[i % PlantPalette.Length];
            }
        }
        return map;
    }

    public static string GetPlantColor(Plant plant, IList<Plant> plantList)
    {
        if (!string.IsNullOrWhiteSpace(plant.Emoji))
        {
            var distinctEmojis = plantList
                .Where(p => !string.IsNullOrWhiteSpace(p.Emoji))
                .Select(p => p.Emoji)
                .Distinct()
                .ToList();
            var idx = distinctEmojis.IndexOf(plant.Emoji);
            return PlantPalette[idx % PlantPalette.Length];
        }
        return PlantPalette[Math.Max(0, plantList.IndexOf(plant)) % PlantPalette.Length];
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
