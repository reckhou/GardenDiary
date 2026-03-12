using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GardenDiary.Helpers;
using GardenDiary.Models;
using GardenDiary.ViewModels;

namespace GardenDiary.Views;

public partial class EditActivityDialog : Window
{
    private List<PlantCheckItem> _allItems = new();
    private List<PlantGroup>     _allGroups = new();
    private readonly IList<Plant>      _plantList;
    private readonly IList<GardenArea> _areas;

    private Plant? _previewPlant;
    private bool   _previewChecked = true;
    private bool   _suppressPreview;

    private IReadOnlyList<(Guid PlantId, bool IsChecked)>? _plantResults;
    /// <summary>All items (checked = has activity, unchecked = remove activity).</summary>
    public IReadOnlyList<(Guid PlantId, bool IsChecked)> PlantResults =>
        _plantResults ??= _allItems.Select(ci => (ci.Plant.Id, ci.IsChecked)).ToList();

    public EditActivityDialog(DateTime date, string activityName,
                              IList<Plant> plants, IList<GardenArea> areas,
                              HashSet<Guid> checkedPlantIds)
    {
        InitializeComponent();

        _plantList = plants;
        _areas     = areas;

        // Header
        var def = MainViewModel.ActivityDefs.FirstOrDefault(d => d.Name == activityName);
        TxtActivityName.Text = activityName;
        TxtDate.Text         = date.ToString("dddd, MMMM d, yyyy");
        if (def != default)
        {
            var bg = (SolidColorBrush)new BrushConverter().ConvertFromString(def.Bg)!;
            var fg = (SolidColorBrush)new BrushConverter().ConvertFromString(def.Fg)!;
            TxtActivityName.Foreground = fg;
            HeaderBorder.Background    = bg;
        }

        // Build check items
        _allItems = plants
            .OrderBy(p => p.CommonName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlantCheckItem(p, checkedPlantIds.Contains(p.Id)))
            .ToList();

        foreach (var ci in _allItems)
            ci.PropertyChanged += OnCheckItemChanged;

        // Build groups
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

        // Show preview for a pre-checked plant
        var firstChecked = _allItems.FirstOrDefault(ci => ci.IsChecked);
        if (firstChecked != null)
        {
            _previewPlant   = firstChecked.Plant;
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

    // ── Search ────────────────────────────────────────────────────────────────

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(TxtSearch.Text);

    private void ApplyFilter(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query)) { PlantGroupList.ItemsSource = _allGroups; return; }

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
        foreach (var ci in visible.SelectMany(g => g.Items)) ci.IsChecked = true;
        _suppressPreview = false;
        _previewChecked = true;
        RefreshPreview();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressPreview = true;
        foreach (var ci in _allItems) ci.IsChecked = false;
        _suppressPreview = false;
        _previewChecked = false;
        RefreshPreview();
    }

    private void OkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
