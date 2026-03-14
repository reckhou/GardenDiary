using System.Windows;
using System.Windows.Controls;
using GardenDiary.Models;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class TaskEditDialog : Window
{
    private List<PlantCheckItem> _allItems  = new();
    private List<PlantGroup>    _allGroups  = new();
    private List<AreaCheckItem> _areaItems  = new();
    private Guid _existingId = Guid.Empty;
    private GardenTaskStatus _existingStatus = GardenTaskStatus.Active;
    private DateTime? _existingCompletedDate;
    private List<Guid> _existingCompletedItemIds = new();

    public GardenTask Task { get; private set; } = new();

    // ── New task ──────────────────────────────────────────────────────────────

    public TaskEditDialog(GardenTask task, IList<Plant> plants, IList<GardenArea> areas)
    {
        InitializeComponent();
        BuildPlantSelector(plants, areas);
        BuildAreaSelector(areas);
        PreFill(task, plants, areas);
    }

    // ── Plant selector (identical grouping logic to CalendarEntryDialog) ───────

    private void BuildPlantSelector(IList<Plant> plants, IList<GardenArea> areas)
    {
        _allItems = plants
            .OrderBy(p => p.CommonName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlantCheckItem(p))
            .ToList();

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
    }

    // ── Area selector ──────────────────────────────────────────────────────────

    private void BuildAreaSelector(IList<GardenArea> areas)
    {
        _areaItems = areas.OrderBy(a => a.Name).Select(a => new AreaCheckItem(a)).ToList();
        AreaCheckList.ItemsSource = _areaItems;
    }

    // ── Pre-fill for edit mode ─────────────────────────────────────────────────

    private void PreFill(GardenTask task, IList<Plant> plants, IList<GardenArea> areas)
    {
        _existingId               = task.Id;
        _existingStatus           = task.Status;
        _existingCompletedDate    = task.CompletedDate;
        _existingCompletedItemIds = new List<Guid>(task.CompletedItemIds);

        TxtTitle.Text            = task.Title;
        DtpReminder.SelectedDate = task.ReminderDate == default ? DateTime.Today : task.ReminderDate;

        if (task.IsLawnTask)
        {
            RbLawn.IsChecked  = true;
            RbPlants.IsChecked = false;
        }

        // Activities
        foreach (var act in task.Activities)
        {
            if (task.IsLawnTask)
            {
                if (act == "Mowing")      ChkMowing.IsChecked      = true;
                if (act == "Watering")    ChkLawnWatering.IsChecked = true;
                if (act == "Overseeding") ChkOverseeding.IsChecked  = true;
                if (act == "Feeding")     ChkFeeding.IsChecked      = true;
                if (act == "Aerating")    ChkAerating.IsChecked     = true;
            }
            else
            {
                if (act == "Watering")               ChkWatering.IsChecked    = true;
                if (act == "Fertilizing & Mulching") ChkFertilizing.IsChecked = true;
                if (act == "Pruning")                ChkPruning.IsChecked     = true;
                if (act == "Planting")               ChkPlanting.IsChecked    = true;
            }
        }

        // Pre-select plants
        foreach (var id in task.PlantIds)
        {
            var item = _allItems.FirstOrDefault(ci => ci.Plant.Id == id);
            if (item != null) item.IsChecked = true;
        }
        // Refresh list to reflect pre-selections
        ApplyFilter(TxtSearch.Text);

        // Pre-select areas
        foreach (var item in _areaItems)
        {
            if (task.AreaIds.Contains(item.Area.Id))
                item.IsChecked = true;
        }

        // Repeat
        if (task.IsRepeating)
        {
            RbRepeat.IsChecked  = true;
            RbOneOff.IsChecked  = false;
            TxtRepeatDays.Text  = task.RepeatDays.ToString();
        }
    }

    // ── Task type toggle ──────────────────────────────────────────────────────

    private void TaskType_Changed(object sender, RoutedEventArgs e)
    {
        if (PnlPlantActivities == null) return; // fires during InitializeComponent before controls are ready
        bool isLawn = RbLawn.IsChecked == true;
        PnlPlantActivities.Visibility  = isLawn ? Visibility.Collapsed  : Visibility.Visible;
        PnlLawnActivities.Visibility   = isLawn ? Visibility.Visible    : Visibility.Collapsed;
        PnlPlantSelector.Visibility    = isLawn ? Visibility.Collapsed  : Visibility.Visible;
        PnlAreaSelector.Visibility     = isLawn ? Visibility.Visible    : Visibility.Collapsed;
    }

    // ── Plant filter ──────────────────────────────────────────────────────────

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
        var visible = (PlantGroupList.ItemsSource as IEnumerable<PlantGroup>) ?? _allGroups;
        foreach (var ci in visible.SelectMany(g => g.Items))
            ci.IsChecked = true;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ci in _allItems) ci.IsChecked = false;
    }

    // ── OK ────────────────────────────────────────────────────────────────────

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text))
        {
            MessageBox.Show("Please enter a title.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (DtpReminder.SelectedDate == null)
        {
            MessageBox.Show("Please select a due date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool isLawn = RbLawn.IsChecked == true;

        var activities = isLawn
            ? GetLawnActivities()
            : GetPlantActivities();

        if (activities.Count == 0)
        {
            MessageBox.Show("Please select at least one activity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var plantIds = _allItems.Where(ci => ci.IsChecked).Select(ci => ci.Plant.Id).ToList();
        var areaIds  = _areaItems.Where(i => i.IsChecked).Select(i => i.Area.Id).ToList();

        if (!isLawn && plantIds.Count == 0)
        {
            MessageBox.Show("Please select at least one plant.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (isLawn && areaIds.Count == 0)
        {
            MessageBox.Show("Please select at least one area.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool isRepeating = RbRepeat.IsChecked == true;
        int repeatDays = 0;
        if (isRepeating)
        {
            if (!int.TryParse(TxtRepeatDays.Text, out repeatDays) || repeatDays <= 0)
            {
                MessageBox.Show("Please enter a valid number of days for the repeat interval.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        Task = new GardenTask
        {
            Id                 = _existingId == Guid.Empty ? Guid.NewGuid() : _existingId,
            Title              = TxtTitle.Text.Trim(),
            ReminderDate       = DtpReminder.SelectedDate.Value.Date,
            IsLawnTask         = isLawn,
            Activities         = activities,
            PlantIds           = plantIds,
            AreaIds            = areaIds,
            IsRepeating        = isRepeating,
            RepeatDays         = repeatDays,
            Status             = _existingStatus,
            CompletedDate      = _existingCompletedDate,
            CompletedItemIds   = _existingCompletedItemIds
        };

        DialogResult = true;
    }

    private List<string> GetPlantActivities()
    {
        var list = new List<string>();
        if (ChkWatering.IsChecked    == true) list.Add("Watering");
        if (ChkFertilizing.IsChecked == true) list.Add("Fertilizing & Mulching");
        if (ChkPruning.IsChecked     == true) list.Add("Pruning");
        if (ChkPlanting.IsChecked    == true) list.Add("Planting");
        return list;
    }

    private List<string> GetLawnActivities()
    {
        var list = new List<string>();
        if (ChkMowing.IsChecked       == true) list.Add("Mowing");
        if (ChkLawnWatering.IsChecked == true) list.Add("Watering");
        if (ChkOverseeding.IsChecked  == true) list.Add("Overseeding");
        if (ChkFeeding.IsChecked      == true) list.Add("Feeding");
        if (ChkAerating.IsChecked     == true) list.Add("Aerating");
        return list;
    }
}
