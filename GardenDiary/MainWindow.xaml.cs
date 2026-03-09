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

    private static readonly string[] PlantPalette =
    [
        "#66BB6A", "#42A5F5", "#FFA726", "#EC407A",
        "#AB47BC", "#26A69A", "#D4E157", "#FF7043",
        "#78909C", "#8D6E63"
    ];

    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged       += OnVmPropertyChanged;
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
        _dragging = null;
        _draggingElement = null;

        var vm = DataContext as MainViewModel;
        if (vm?.SelectedArea == null) return;

        var area      = vm.SelectedArea;
        var plantList = vm.Plants.ToList();

        GardenCanvas.Width  = area.Width;
        GardenCanvas.Height = area.Height;

        foreach (var placement in area.PlantPlacements)
        {
            var plant = plantList.FirstOrDefault(p => p.Id == placement.PlantId);
            if (plant == null) continue;

            var colorHex  = PlantPalette[plantList.IndexOf(plant) % PlantPalette.Length];
            var fillColor = (Color)ColorConverter.ConvertFromString(colorHex);
            var isSelected = vm.SelectedPlacement?.Id == placement.Id;

            // Container grid (hit-test target + drag handle)
            var grid = new Grid
            {
                Width  = placement.Radius * 2,
                Height = placement.Radius * 2,
                Cursor = Cursors.SizeAll,
                Tag    = placement.Id
            };

            // Circle
            var ellipse = new Ellipse
            {
                Fill            = new SolidColorBrush(fillColor) { Opacity = 0.78 },
                Stroke          = isSelected ? Brushes.White : new SolidColorBrush(fillColor) { Opacity = 0.4 },
                StrokeThickness = isSelected ? 3 : 1.5,
                Width           = placement.Radius * 2,
                Height          = placement.Radius * 2
            };

            // Label
            var label = new TextBlock
            {
                Text                = plant.CommonName,
                FontSize            = Math.Clamp(placement.Radius * 0.38, 8, 14),
                Foreground          = Brushes.White,
                TextWrapping        = TextWrapping.Wrap,
                TextAlignment       = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Width               = placement.Radius * 2 - 6,
                IsHitTestVisible    = false
            };

            grid.Children.Add(ellipse);
            grid.Children.Add(label);

            Canvas.SetLeft(grid, placement.X - placement.Radius);
            Canvas.SetTop(grid,  placement.Y - placement.Radius);

            grid.MouseLeftButtonDown += PlantPanel_MouseLeftButtonDown;
            grid.MouseMove           += PlantPanel_MouseMove;
            grid.MouseLeftButtonUp   += PlantPanel_MouseLeftButtonUp;

            GardenCanvas.Children.Add(grid);
        }
    }

    // Click on empty canvas → place selected plant
    private void GardenCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != GardenCanvas) return;

        var vm = DataContext as MainViewModel;
        if (vm?.PlantToPlace == null || vm.SelectedArea == null) return;

        var pos       = e.GetPosition(GardenCanvas);
        var placement = vm.AddPlacement(pos.X, pos.Y);
        if (placement == null) return;

        RefreshGardenCanvas();
        vm.SelectedPlacement = placement;
        RefreshGardenCanvas(); // re-render with selection highlight
    }

    // Click / drag start on a plant circle
    private void PlantPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;

        var vm          = DataContext as MainViewModel;
        var placementId = (Guid)el.Tag;
        var placement   = vm?.SelectedArea?.PlantPlacements.FirstOrDefault(p => p.Id == placementId);
        if (placement == null) return;

        _dragging        = placement;
        _draggingElement = el;
        _dragOffset      = new Point(e.GetPosition(GardenCanvas).X - placement.X,
                                     e.GetPosition(GardenCanvas).Y - placement.Y);

        el.CaptureMouse();

        if (vm != null) vm.SelectedPlacement = placement;
        RefreshGardenCanvas(); // show selection ring

        e.Handled = true; // prevent canvas click handler
    }

    // Drag move
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

    // Drop
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
