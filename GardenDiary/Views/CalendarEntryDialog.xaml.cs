using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GardenDiary.Helpers;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

// ── Helper types ──────────────────────────────────────────────────────────────

public class PlantCheckItem : INotifyPropertyChanged
{
    public Plant Plant { get; }

    public string LatinDisplay
    {
        get
        {
            var hasLatin   = !string.IsNullOrWhiteSpace(Plant.LatinName);
            var hasVariety = !string.IsNullOrWhiteSpace(Plant.Variety);
            if (hasLatin && hasVariety) return $"({Plant.LatinName} '{Plant.Variety}')";
            if (hasLatin)               return $"({Plant.LatinName})";
            if (hasVariety)             return $"('{Plant.Variety}')";
            return "";
        }
    }

    public Visibility HasLatinDisplay => string.IsNullOrEmpty(LatinDisplay) ? Visibility.Collapsed : Visibility.Visible;

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public PlantCheckItem(Plant plant, bool isChecked = false)
    {
        Plant = plant;
        _isChecked = isChecked;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class PlantGroup
{
    public string GroupName { get; set; } = "";
    public List<PlantCheckItem> Items { get; set; } = new();
}

// ── Dialog ────────────────────────────────────────────────────────────────────

public partial class CalendarEntryDialog : Window
{
    private List<PlantCheckItem> _allItems = new();
    private List<PlantGroup> _allGroups = new();
    private readonly IList<Plant> _plantList;
    private readonly IList<GardenArea> _areas;
    private bool _suppressPreview;

    // Last interacted plant (drives preview)
    private Plant? _previewPlant;
    private bool   _previewChecked = true; // false = unchecked (shown in red)

    public List<Plant> SelectedPlants { get; private set; } = new();
    public DiaryEntry Entry { get; private set; } = new();

    public CalendarEntryDialog(DateTime date, IList<Plant> plants, IList<GardenArea> areas, Plant? preselect = null)
    {
        InitializeComponent();

        _plantList = plants;
        _areas     = areas;

        DtpDate.SelectedDate = date.Date;

        // Build check items
        _allItems = plants
            .OrderBy(p => p.CommonName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlantCheckItem(p, p == preselect))
            .ToList();

        // Subscribe to checkbox changes for preview
        foreach (var ci in _allItems)
            ci.PropertyChanged += OnCheckItemChanged;

        // Build groups: areas (alphabetical), then "Not Placed"
        var placedIds = areas
            .SelectMany(a => a.PlantPlacements.Select(pp => pp.PlantId))
            .ToHashSet();

        _allGroups = new List<PlantGroup>();

        foreach (var area in areas.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var areaPlantIds = area.PlantPlacements.Select(pp => pp.PlantId).ToHashSet();
            var items = _allItems.Where(ci => areaPlantIds.Contains(ci.Plant.Id)).ToList();
            if (items.Count > 0)
                _allGroups.Add(new PlantGroup { GroupName = area.Name, Items = items });
        }

        var notPlaced = _allItems.Where(ci => !placedIds.Contains(ci.Plant.Id)).ToList();
        if (notPlaced.Count > 0)
            _allGroups.Add(new PlantGroup { GroupName = "Not Placed", Items = notPlaced });

        if (_allGroups.Count == 0 && _allItems.Count > 0)
            _allGroups.Add(new PlantGroup { GroupName = "All Plants", Items = _allItems });

        ApplyFilter("");

        // Show preview for preselected plant if it has a location
        if (preselect != null)
        {
            _previewPlant   = preselect;
            _previewChecked = true;
            Loaded += (_, _) => RefreshPreview();
        }
    }

    // ── Checkbox change → preview ─────────────────────────────────────────────

    private void OnCheckItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlantCheckItem.IsChecked)) return;
        if (sender is not PlantCheckItem ci) return;
        if (_suppressPreview) return;

        _previewPlant   = ci.Plant;
        _previewChecked = ci.IsChecked;
        RefreshPreview();
    }

    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshPreview();

    private void RefreshPreview()
    {
        PreviewCanvas.Children.Clear();

        if (_previewPlant == null) { PreviewBorder.Visibility = Visibility.Collapsed; return; }

        var area = _areas.FirstOrDefault(a => a.PlantPlacements.Any(p => p.PlantId == _previewPlant.Id));
        if (area == null) { PreviewBorder.Visibility = Visibility.Collapsed; return; }

        PreviewBorder.Visibility = Visibility.Visible;
        PreviewLabel.Text       = _previewChecked
            ? $"Located in: {area.Name}"
            : $"Located in: {area.Name}  (deselected)";
        PreviewLabel.Foreground = _previewChecked
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55))
            : System.Windows.Media.Brushes.Red;

        GardenPreviewHelper.Draw(PreviewCanvas, _previewPlant, area, _plantList, _previewChecked);
    }

    // ── Search / filter ───────────────────────────────────────────────────────

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(TxtSearch.Text);

    private void ApplyFilter(string query)
    {
        query = query.Trim();

        if (string.IsNullOrEmpty(query))
        {
            PlantGroupList.ItemsSource = _allGroups;
            return;
        }

        var filtered = _allGroups
            .Select(g => new PlantGroup
            {
                GroupName = g.GroupName,
                Items = g.Items.Where(ci =>
                    ci.Plant.CommonName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    (ci.Plant.Variety   ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    (ci.Plant.LatinName ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase)
                ).ToList()
            })
            .Where(g => g.Items.Count > 0)
            .ToList();

        PlantGroupList.ItemsSource = filtered;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressPreview = true;
        var visible = (PlantGroupList.ItemsSource as IEnumerable<PlantGroup>) ?? _allGroups;
        foreach (var ci in visible.SelectMany(g => g.Items))
            ci.IsChecked = true;
        _suppressPreview = false;
        _previewChecked = true;
        RefreshPreview();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressPreview = true;
        foreach (var ci in _allItems)
            ci.IsChecked = false;
        _suppressPreview = false;
        _previewChecked = false;
        RefreshPreview();
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────────

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (DtpDate.SelectedDate == null)
        {
            MessageBox.Show("Please select a date.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedPlants = _allItems.Where(ci => ci.IsChecked).Select(ci => ci.Plant).ToList();

        if (SelectedPlants.Count == 0)
        {
            MessageBox.Show("Please select at least one plant.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool anyActivity = ChkWatering.IsChecked == true || ChkFertilizing.IsChecked == true ||
                           ChkPruning.IsChecked == true || ChkPlanting.IsChecked == true;
        if (!anyActivity)
        {
            MessageBox.Show("Please select at least one activity.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool fertMulch = ChkFertilizing.IsChecked == true;
        Entry = new DiaryEntry
        {
            Date        = DtpDate.SelectedDate.Value.Date,
            Watering    = ChkWatering.IsChecked == true,
            Fertilizing = fertMulch,
            Mulching    = fertMulch,
            Pruning     = ChkPruning.IsChecked  == true,
            Planting    = ChkPlanting.IsChecked == true,
            Notes       = TxtNotes.Text.Trim()
        };

        DialogResult = true;
    }
}
