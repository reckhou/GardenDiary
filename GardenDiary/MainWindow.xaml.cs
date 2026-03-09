using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GardenDiary.Models;
using GardenDiary.ViewModels;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GardenDiary;

public partial class MainWindow : Window
{
    // ── Canvas drag state ─────────────────────────────────────────────────────
    private PlantPlacement? _dragging;
    private FrameworkElement? _draggingElement;
    private Point _dragOffset;

    // Keeps references to canvas elements for direct manipulation
    private readonly Dictionary<Guid, Grid>    _placementGrids    = new();
    private readonly Dictionary<Guid, Ellipse> _placementEllipses = new();

    // ── Zoom state ────────────────────────────────────────────────────────────
    private double _zoom = 1.0;
    private const double ZoomMin = 0.1;
    private const double ZoomMax = 10.0;
    private readonly ScaleTransform _scaleTransform = new(1, 1);

    // ── Pan state (middle-mouse) ───────────────────────────────────────────────
    private bool _isPanning;
    private Point _panStartViewport;
    private double _panScrollH;
    private double _panScrollV;

    private static readonly string[] PlantPalette =
    [
        "#66BB6A", "#42A5F5", "#FFA726", "#EC407A",
        "#AB47BC", "#26A69A", "#D4E157", "#FF7043",
        "#78909C", "#8D6E63"
    ];

    public MainWindow()
    {
        InitializeComponent();
        GardenCanvas.LayoutTransform = _scaleTransform;

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged        += OnVmPropertyChanged;
            vm.CanvasRefreshRequested += RefreshGardenCanvas;
        }
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    private void Cal_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Calendar cal)
            cal.SelectedDate = DateTime.Today;
    }

    private void Cal_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Calendar cal)
            vm.SelectedCalendarDate = cal.SelectedDate;
    }

    // ── Garden canvas ─────────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedArea))
            RefreshGardenCanvas();
    }

    private void GardenCanvas_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue) RefreshGardenCanvas();
    }

    private void RefreshGardenCanvas()
    {
        GardenCanvas.Children.Clear();
        _placementGrids.Clear();
        _placementEllipses.Clear();
        _dragging = null;
        _draggingElement = null;

        var vm = DataContext as MainViewModel;
        if (vm?.SelectedArea == null) return;

        var area      = vm.SelectedArea;
        var plantList = vm.Plants.ToList();

        GardenCanvas.Width  = area.Width;
        GardenCanvas.Height = area.Height;

        // ── Grid lines every 50 cm ────────────────────────────────────────────
        const double gridStep = 50;
        var gridBrush  = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0));
        var labelBrush = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));

        for (double x = gridStep; x < area.Width; x += gridStep)
        {
            GardenCanvas.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = area.Height,
                Stroke = gridBrush, StrokeThickness = 0.5,
                IsHitTestVisible = false
            });
            var lbl = new TextBlock
            {
                Text = $"{x:0} cm", FontSize = 9, Foreground = labelBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, x + 2);
            Canvas.SetTop(lbl, 2);
            GardenCanvas.Children.Add(lbl);
        }

        for (double y = gridStep; y < area.Height; y += gridStep)
        {
            GardenCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = area.Width, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 0.5,
                IsHitTestVisible = false
            });
            var lbl = new TextBlock
            {
                Text = $"{y:0} cm", FontSize = 9, Foreground = labelBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, 2);
            Canvas.SetTop(lbl, y + 2);
            GardenCanvas.Children.Add(lbl);
        }

        // ── Plant circles ─────────────────────────────────────────────────────
        foreach (var placement in area.PlantPlacements)
        {
            var plant = plantList.FirstOrDefault(p => p.Id == placement.PlantId);
            if (plant == null) continue;

            var colorHex  = PlantPalette[plantList.IndexOf(plant) % PlantPalette.Length];
            var fillColor = (Color)ColorConverter.ConvertFromString(colorHex);
            var isSelected = vm.SelectedPlacement?.Id == placement.Id;

            var grid = new Grid
            {
                Width  = placement.Radius * 2,
                Height = placement.Radius * 2,
                Cursor = Cursors.SizeAll,
                Tag    = placement.Id
            };

            var ellipse = new Ellipse
            {
                Fill            = new SolidColorBrush(fillColor) { Opacity = 0.78 },
                Stroke          = isSelected ? Brushes.White : new SolidColorBrush(fillColor) { Opacity = 0.4 },
                StrokeThickness = isSelected ? 3 : 1.5,
                Width           = placement.Radius * 2,
                Height          = placement.Radius * 2
            };

            // Two-line label: common name + latin name
            var labelPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Width               = placement.Radius * 2 - 6,
                IsHitTestVisible    = false
            };
            labelPanel.Children.Add(new TextBlock
            {
                Text          = plant.CommonName,
                FontSize      = Math.Clamp(placement.Radius * 0.38, 7, 13),
                Foreground    = Brushes.White,
                FontWeight    = FontWeights.SemiBold,
                TextWrapping  = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            });
            if (!string.IsNullOrWhiteSpace(plant.LatinName))
            {
                labelPanel.Children.Add(new TextBlock
                {
                    Text          = plant.LatinName,
                    FontSize      = Math.Clamp(placement.Radius * 0.28, 6, 10),
                    Foreground    = new SolidColorBrush(Colors.White) { Opacity = 0.82 },
                    FontStyle     = FontStyles.Italic,
                    TextWrapping  = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false
                });
            }
            if (!string.IsNullOrWhiteSpace(plant.Variety))
            {
                labelPanel.Children.Add(new TextBlock
                {
                    Text          = plant.Variety,
                    FontSize      = Math.Clamp(placement.Radius * 0.26, 6, 9),
                    Foreground    = new SolidColorBrush(Colors.White) { Opacity = 0.70 },
                    TextWrapping  = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false
                });
            }

            grid.Children.Add(ellipse);
            grid.Children.Add(labelPanel);

            Canvas.SetLeft(grid, placement.X - placement.Radius);
            Canvas.SetTop(grid,  placement.Y - placement.Radius);

            grid.MouseLeftButtonDown += PlantPanel_MouseLeftButtonDown;
            grid.MouseMove           += PlantPanel_MouseMove;
            grid.MouseLeftButtonUp   += PlantPanel_MouseLeftButtonUp;

            GardenCanvas.Children.Add(grid);

            _placementGrids[placement.Id]    = grid;
            _placementEllipses[placement.Id] = ellipse;
        }
    }

    // Update only the selection ring — avoids rebuilding the canvas
    private void UpdateSelectionRing(Guid? deselectedId, Guid? selectedId)
    {
        var vm        = DataContext as MainViewModel;
        var plantList = vm?.Plants.ToList() ?? [];

        if (deselectedId.HasValue && _placementEllipses.TryGetValue(deselectedId.Value, out var oldEllipse))
        {
            var pl = vm?.SelectedArea?.PlantPlacements.FirstOrDefault(p => p.Id == deselectedId.Value);
            if (pl != null)
            {
                var plant    = plantList.FirstOrDefault(p => p.Id == pl.PlantId);
                var colorHex = plant != null ? PlantPalette[plantList.IndexOf(plant) % PlantPalette.Length] : "#66BB6A";
                var col      = (Color)ColorConverter.ConvertFromString(colorHex);
                oldEllipse.Stroke          = new SolidColorBrush(col) { Opacity = 0.4 };
                oldEllipse.StrokeThickness = 1.5;
            }
        }

        if (selectedId.HasValue && _placementEllipses.TryGetValue(selectedId.Value, out var newEllipse))
        {
            newEllipse.Stroke          = Brushes.White;
            newEllipse.StrokeThickness = 3;
        }
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    private void ApplyZoom(double newZoom, Point? viewportFocus = null)
    {
        newZoom = Math.Clamp(newZoom, ZoomMin, ZoomMax);
        if (Math.Abs(newZoom - _zoom) < 1e-9) return;

        var sv     = GardenScrollViewer;
        var focusX = viewportFocus?.X ?? sv.ViewportWidth  / 2;
        var focusY = viewportFocus?.Y ?? sv.ViewportHeight / 2;

        // Canvas coordinate under the focus point before zoom
        var canvasX = (sv.HorizontalOffset + focusX) / _zoom;
        var canvasY = (sv.VerticalOffset   + focusY) / _zoom;

        _zoom = newZoom;
        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;
        ZoomLabel.Text = $"{_zoom * 100:F0}%";

        sv.UpdateLayout();
        sv.ScrollToHorizontalOffset(canvasX * _zoom - focusX);
        sv.ScrollToVerticalOffset(canvasY   * _zoom - focusY);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)    => ApplyZoom(_zoom * 1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e)   => ApplyZoom(_zoom / 1.25);
    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _zoom = 1.0;
        _scaleTransform.ScaleX = 1;
        _scaleTransform.ScaleY = 1;
        ZoomLabel.Text = "100%";
        GardenScrollViewer.ScrollToHome();
    }

    private void GardenScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.12 : 1.0 / 1.12;
        ApplyZoom(_zoom * factor, e.GetPosition(GardenScrollViewer));
        e.Handled = true; // prevent ScrollViewer default vertical scroll
    }

    // ── Middle-mouse pan ──────────────────────────────────────────────────────

    private void GardenScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        _isPanning        = true;
        _panStartViewport = e.GetPosition(GardenScrollViewer);
        _panScrollH       = GardenScrollViewer.HorizontalOffset;
        _panScrollV       = GardenScrollViewer.VerticalOffset;
        GardenScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void GardenScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(GardenScrollViewer);
        GardenScrollViewer.ScrollToHorizontalOffset(_panScrollH - (pos.X - _panStartViewport.X));
        GardenScrollViewer.ScrollToVerticalOffset  (_panScrollV - (pos.Y - _panStartViewport.Y));
    }

    private void GardenScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning || e.ChangedButton != MouseButton.Middle) return;
        _isPanning = false;
        GardenScrollViewer.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ── Plant placement (canvas click) ────────────────────────────────────────

    private void GardenCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != GardenCanvas) return;

        var vm = DataContext as MainViewModel;
        if (vm?.PlantToPlace == null || vm.SelectedArea == null) return;

        var pos       = e.GetPosition(GardenCanvas);
        var placement = vm.AddPlacement(pos.X, pos.Y);
        if (placement == null) return;

        vm.SelectedPlacement = placement;
        RefreshGardenCanvas();
    }

    // ── Plant drag / double-click ─────────────────────────────────────────────

    private void PlantPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;

        var vm          = DataContext as MainViewModel;
        var placementId = (Guid)el.Tag;
        var placement   = vm?.SelectedArea?.PlantPlacements.FirstOrDefault(p => p.Id == placementId);
        if (placement == null) return;

        // Double-click → open Add Activity dialog, don't start drag
        if (e.ClickCount == 2)
        {
            StopDrag();
            vm!.OpenActivityDialogForPlacement(placement.PlantId);
            e.Handled = true;
            return;
        }

        _dragging        = placement;
        _draggingElement = el;
        _dragOffset      = new Point(e.GetPosition(GardenCanvas).X - placement.X,
                                     e.GetPosition(GardenCanvas).Y - placement.Y);

        el.CaptureMouse();

        var oldId = vm?.SelectedPlacement?.Id;
        if (vm != null) vm.SelectedPlacement = placement;
        UpdateSelectionRing(oldId, placement.Id);

        e.Handled = true;
    }

    private void PlantPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging == null || _draggingElement == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { StopDrag(); return; }

        var area = (DataContext as MainViewModel)?.SelectedArea;
        if (area == null) return;

        var pos  = e.GetPosition(GardenCanvas);
        var newX = Math.Clamp(pos.X - _dragOffset.X, _dragging.Radius, area.Width  - _dragging.Radius);
        var newY = Math.Clamp(pos.Y - _dragOffset.Y, _dragging.Radius, area.Height - _dragging.Radius);

        _dragging.X = newX;
        _dragging.Y = newY;
        Canvas.SetLeft(_draggingElement, newX - _dragging.Radius);
        Canvas.SetTop(_draggingElement,  newY - _dragging.Radius);
    }

    private void PlantPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging == null) return;
        StopDrag();
    }

    private void StopDrag()
    {
        _draggingElement?.ReleaseMouseCapture();
        _dragging        = null;
        _draggingElement = null;
        (DataContext as MainViewModel)?.SaveAreas();
    }
}
