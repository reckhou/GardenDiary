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
using MenuItem = System.Windows.Controls.MenuItem;
using Panel = System.Windows.Controls.Panel;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace GardenDiary;

public partial class MainWindow : Window
{
    // ── Canvas drag state ─────────────────────────────────────────────────────
    private PlantPlacement? _dragging;
    private FrameworkElement? _draggingElement;
    private Point _dragOffset;

    // Shape drag state
    private GardenShape? _draggingShape;
    private FrameworkElement? _draggingShapeElement;
    private Point _shapeDragOffset;

    // Keeps references to canvas elements for direct manipulation
    private readonly Dictionary<Guid, Grid>    _placementGrids    = new();
    private readonly Dictionary<Guid, Ellipse> _placementEllipses = new();
    private readonly Dictionary<Guid, FrameworkElement> _shapeElements = new();

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

    /// <summary>
    /// Returns the colour hex for a plant. Plants sharing the same non-empty emoji
    /// get the same colour; others fall back to their index in the plant list.
    /// </summary>
    private static string GetPlantColor(Plant plant, List<Plant> plantList)
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
        return PlantPalette[plantList.IndexOf(plant) % PlantPalette.Length];
    }

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
        _shapeElements.Clear();
        _dragging = null;
        _draggingElement = null;
        _draggingShape = null;
        _draggingShapeElement = null;

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

        // ── Shapes (drawn below plants, sorted by ZIndex) ────────────────────
        foreach (var shape in area.Shapes.OrderBy(s => s.ZIndex))
        {
            AddShapeToCanvas(shape, vm);
        }

        // ── Plant circles ─────────────────────────────────────────────────────
        foreach (var placement in area.PlantPlacements)
        {
            var plant = plantList.FirstOrDefault(p => p.Id == placement.PlantId);
            if (plant == null) continue;

            var colorHex = GetPlantColor(plant, plantList);
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
                Stroke          = isSelected ? Brushes.Black : new SolidColorBrush(fillColor) { Opacity = 0.4 },
                StrokeThickness = isSelected ? 2.5 : 1.5,
                Width           = placement.Radius * 2,
                Height          = placement.Radius * 2
            };

            var labelPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Width               = placement.Radius * 2 - 6,
                IsHitTestVisible    = false
            };
            if (!string.IsNullOrWhiteSpace(plant.Emoji))
            {
                labelPanel.Children.Add(new Emoji.Wpf.TextBlock
                {
                    Text          = plant.Emoji,
                    FontSize      = Math.Clamp(placement.Radius * 0.5, 10, 22),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false
                });
            }
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

        // Sync shape color combo if a shape is selected
        SyncShapeColorCombo();
    }

    // ── Shapes on canvas ─────────────────────────────────────────────────────

    private void AddShapeToCanvas(GardenShape shape, MainViewModel vm)
    {
        var fillColor = (Color)ColorConverter.ConvertFromString(shape.FillColor);
        var isSelected = vm.SelectedShape?.Id == shape.Id;

        FrameworkElement shapeVisual;
        if (shape.Type == ShapeType.Circle)
        {
            shapeVisual = new Ellipse
            {
                Width           = shape.Width,
                Height          = shape.Height,
                Fill            = new SolidColorBrush(fillColor) { Opacity = shape.Opacity },
                Stroke          = isSelected ? Brushes.Black : new SolidColorBrush(fillColor) { Opacity = 0.6 },
                StrokeThickness = isSelected ? 2.5 : 1,
                StrokeDashArray = isSelected ? null : new DoubleCollection([4, 2])
            };
        }
        else
        {
            shapeVisual = new Rectangle
            {
                Width           = shape.Width,
                Height          = shape.Height,
                Fill            = new SolidColorBrush(fillColor) { Opacity = shape.Opacity },
                Stroke          = isSelected ? Brushes.Black : new SolidColorBrush(fillColor) { Opacity = 0.6 },
                StrokeThickness = isSelected ? 2.5 : 1,
                StrokeDashArray = isSelected ? null : new DoubleCollection([4, 2])
            };
        }

        // Wrap in a Grid so we can overlay a label
        var container = new Grid
        {
            Width  = shape.Width,
            Height = shape.Height,
            Cursor = Cursors.SizeAll,
            Tag    = shape.Id
        };
        container.Children.Add(shapeVisual);

        if (!string.IsNullOrWhiteSpace(shape.Label))
        {
            container.Children.Add(new TextBlock
            {
                Text              = shape.Label,
                FontSize          = Math.Clamp(Math.Min(shape.Width, shape.Height) * 0.18, 8, 14),
                Foreground        = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                FontWeight        = FontWeights.SemiBold,
                TextWrapping      = TextWrapping.Wrap,
                TextAlignment     = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                IsHitTestVisible  = false,
                MaxWidth          = shape.Width - 8
            });
        }

        Canvas.SetLeft(container, shape.X);
        Canvas.SetTop(container, shape.Y);
        Panel.SetZIndex(container, shape.ZIndex);

        container.MouseLeftButtonDown += ShapeElement_MouseLeftButtonDown;
        container.MouseMove           += ShapeElement_MouseMove;
        container.MouseLeftButtonUp   += ShapeElement_MouseLeftButtonUp;
        container.MouseRightButtonUp  += ShapeElement_MouseRightButtonUp;

        GardenCanvas.Children.Add(container);
        _shapeElements[shape.Id] = container;
    }

    private void ShapeElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        var vm      = DataContext as MainViewModel;
        var shapeId = (Guid)el.Tag;
        var shape   = vm?.SelectedArea?.Shapes.FirstOrDefault(s => s.Id == shapeId);
        if (shape == null) return;

        var prevShapeId = vm?.SelectedShape?.Id;
        var prevPlantId = vm?.SelectedPlacement?.Id;

        _draggingShape        = shape;
        _draggingShapeElement = el;
        _shapeDragOffset      = new Point(e.GetPosition(GardenCanvas).X - shape.X,
                                          e.GetPosition(GardenCanvas).Y - shape.Y);
        el.CaptureMouse();

        // Select this shape, deselect plant — update visuals directly, no canvas rebuild
        if (vm != null)
        {
            vm.SelectedPlacement = null;
            vm.SelectedShape = shape;
        }
        UpdateSelectionRing(prevPlantId, null);
        UpdateShapeSelectionRing(prevShapeId, shape.Id);
        SyncShapeColorCombo();
        e.Handled = true;
    }

    private void ShapeElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingShape == null || _draggingShapeElement == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { StopShapeDrag(); return; }

        var area = (DataContext as MainViewModel)?.SelectedArea;
        if (area == null) return;

        var pos  = e.GetPosition(GardenCanvas);
        var newX = Math.Clamp(pos.X - _shapeDragOffset.X, 0, area.Width  - _draggingShape.Width);
        var newY = Math.Clamp(pos.Y - _shapeDragOffset.Y, 0, area.Height - _draggingShape.Height);

        _draggingShape.X = newX;
        _draggingShape.Y = newY;
        Canvas.SetLeft(_draggingShapeElement, newX);
        Canvas.SetTop(_draggingShapeElement,  newY);
    }

    private void ShapeElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingShape == null) return;
        StopShapeDrag();
    }

    private void StopShapeDrag()
    {
        _draggingShapeElement?.ReleaseMouseCapture();
        _draggingShape        = null;
        _draggingShapeElement = null;
        (DataContext as MainViewModel)?.SaveAreas();
    }

    private void ShapeElement_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        var vm      = DataContext as MainViewModel;
        var shapeId = (Guid)el.Tag;
        var shape   = vm?.SelectedArea?.Shapes.FirstOrDefault(s => s.Id == shapeId);
        if (shape == null || vm == null) return;

        vm.SelectedPlacement = null;
        vm.SelectedShape = shape;

        var ctx = new ContextMenu();

        var bringFront = new MenuItem { Header = "Bring to Front" };
        bringFront.Click += (_, _) => { vm.BringShapeToFrontCommand.Execute(null); };
        ctx.Items.Add(bringFront);

        var sendBack = new MenuItem { Header = "Send to Back" };
        sendBack.Click += (_, _) => { vm.SendShapeToBackCommand.Execute(null); };
        ctx.Items.Add(sendBack);

        ctx.Items.Add(new Separator());

        var remove = new MenuItem { Header = "Remove" };
        remove.Click += (_, _) => { vm.DeleteShapeCommand.Execute(null); };
        ctx.Items.Add(remove);

        ctx.IsOpen = true;
        e.Handled = true;
    }

    // ── Shape property editors ───────────────────────────────────────────────

    private void ShapeField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            var binding = tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            binding?.UpdateSource();
        }
        SaveAndRefreshShape();
    }

    private void ShapeField_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is System.Windows.Controls.TextBox tb)
        {
            var binding = tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            binding?.UpdateSource();
        }
        SaveAndRefreshShape();
        e.Handled = true;
    }

    private void SaveAndRefreshShape()
    {
        var vm = DataContext as MainViewModel;
        if (vm?.SelectedShape == null) return;
        vm.SaveAreas();
        vm.OnPropertyChanged(nameof(vm.SelectedShapeLabel));
        RefreshGardenCanvas();
    }

    private void ShapeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox combo) return;
        if (combo.SelectedItem is not ComboBoxItem item) return;
        var vm = DataContext as MainViewModel;
        if (vm?.SelectedShape == null) return;
        var hex = item.Tag as string;
        if (hex == null || hex == vm.SelectedShape.FillColor) return;
        vm.SelectedShape.FillColor = hex;
        vm.SaveAreas();
        RefreshGardenCanvas();
    }

    private void SyncShapeColorCombo()
    {
        var vm = DataContext as MainViewModel;
        if (vm?.SelectedShape == null) return;
        foreach (ComboBoxItem item in ShapeColorCombo.Items)
        {
            if (item.Tag is string hex && hex == vm.SelectedShape.FillColor)
            {
                ShapeColorCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void UpdateShapeSelectionRing(Guid? deselectedId, Guid? selectedId)
    {
        var vm = DataContext as MainViewModel;
        if (deselectedId.HasValue && _shapeElements.TryGetValue(deselectedId.Value, out var oldEl))
        {
            var shape = vm?.SelectedArea?.Shapes.FirstOrDefault(s => s.Id == deselectedId.Value);
            if (shape != null && oldEl is Grid oldGrid && oldGrid.Children[0] is Shape oldShape)
            {
                var col = (Color)ColorConverter.ConvertFromString(shape.FillColor);
                oldShape.Stroke          = new SolidColorBrush(col) { Opacity = 0.6 };
                oldShape.StrokeThickness = 1;
                oldShape.StrokeDashArray = new DoubleCollection([4, 2]);
            }
        }
        if (selectedId.HasValue && _shapeElements.TryGetValue(selectedId.Value, out var newEl))
        {
            if (newEl is Grid newGrid && newGrid.Children[0] is Shape newShape)
            {
                newShape.Stroke          = Brushes.Black;
                newShape.StrokeThickness = 2.5;
                newShape.StrokeDashArray = null;
            }
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
                var colorHex = plant != null ? GetPlantColor(plant, plantList) : "#66BB6A";
                var col      = (Color)ColorConverter.ConvertFromString(colorHex);
                oldEllipse.Stroke          = new SolidColorBrush(col) { Opacity = 0.4 };
                oldEllipse.StrokeThickness = 1.5;
            }
        }

        if (selectedId.HasValue && _placementEllipses.TryGetValue(selectedId.Value, out var newEllipse))
        {
            newEllipse.Stroke          = Brushes.Black;
            newEllipse.StrokeThickness = 2.5;
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
        e.Handled = true;
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

    private void DeselectAll()
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;
        var oldPlantId = vm.SelectedPlacement?.Id;
        var oldShapeId = vm.SelectedShape?.Id;
        vm.SelectedPlacement = null;
        vm.SelectedShape = null;
        UpdateSelectionRing(oldPlantId, null);
        UpdateShapeSelectionRing(oldShapeId, null);
    }

    private void GardenCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != GardenCanvas) return;

        var vm = DataContext as MainViewModel;

        // Deselect everything when clicking empty canvas
        DeselectAll();

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

        var oldPlantId = vm?.SelectedPlacement?.Id;
        var oldShapeId = vm?.SelectedShape?.Id;
        if (vm != null)
        {
            vm.SelectedShape = null;
            vm.SelectedPlacement = placement;
        }
        UpdateShapeSelectionRing(oldShapeId, null);
        UpdateSelectionRing(oldPlantId, placement.Id);

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
