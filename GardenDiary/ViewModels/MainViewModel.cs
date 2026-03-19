using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GardenDiary.Models;
using GardenDiary.Services;
using GardenDiary.Views;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private DataService _dataService = new(DataService.LoadCustomDir());
    private BackupService _backupService = null!;
    private readonly WeatherService _weatherService = new();

    private Plant? _selectedPlant;
    private DiaryEntry? _selectedEntry;
    private string _statusText = "Ready";
    private DateTime? _selectedCalendarDate;
    private DayWeather? _dayWeather;
    private bool _isLoadingWeather;
    private bool _showCompletedTasks;

    // Garden planner
    private GardenArea? _selectedArea;
    private Plant? _plantToPlace;
    private PlantOption? _selectedPlantOption;
    private double _defaultRadius = 30;
    private PlantPlacement? _selectedPlacement;
    private GardenShape? _selectedShape;

    public ObservableCollection<Plant> Plants { get; } = new();
    public ObservableCollection<Plant> FilteredPlants { get; } = new();
    public ObservableCollection<DiaryEntry> Entries { get; } = new();
    public ObservableCollection<DayTaskGroup> DayTasks { get; } = new();
    public ObservableCollection<AreaSummary> DayAreaTasks { get; } = new();
    public ObservableCollection<GardenArea> Areas { get; } = new();
    public ObservableCollection<PlantOption> PlantOptions { get; } = new();
    public IEnumerable<PlantOption> UnplacedPlantOptions => PlantOptions.Where(o => o.IsAvailable);
    public HashSet<DateTime> DaysWithActivities { get; } = new();
    public ObservableCollection<PlantFilterItem> PlantFilterItems { get; } = new();

    public ObservableCollection<GardenTask>            Tasks               { get; } = new();
    public ObservableCollection<TaskCardViewModel>     TaskCards           { get; } = new();
    public ObservableCollection<TaskCardViewModel>     CompletedTaskCards  { get; } = new();
    public ObservableCollection<PlantAreaGroupViewModel> PlantAreaGroups { get; } = new();
    public ObservableCollection<ReminderAreaViewModel>   AreaReminders   { get; } = new();

    private PlantFilterItem? _selectedPlantFilter;
    public PlantFilterItem? SelectedPlantFilter
    {
        get => _selectedPlantFilter;
        set
        {
            _selectedPlantFilter = value;
            OnPropertyChanged();
            RefreshFilteredPlants();
        }
    }

    private string _plantSearchText = "";
    public string PlantSearchText
    {
        get => _plantSearchText;
        set { _plantSearchText = value; OnPropertyChanged(); RefreshFilteredPlants(); }
    }

    private string _reminderSearchText = "";
    public string ReminderSearchText
    {
        get => _reminderSearchText;
        set { _reminderSearchText = value; OnPropertyChanged(); RebuildReminders(); }
    }

    // ── Plants & Diary ────────────────────────────────────────────────────────

    public Plant? SelectedPlant
    {
        get => _selectedPlant;
        set
        {
            _selectedPlant = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPlantTitle));
            OnPropertyChanged(nameof(SelectedPlantLocationArea));
            OnPropertyChanged(nameof(SelectedPlantHasLocation));
            OnPropertyChanged(nameof(SelectedPlantLocationLabel));
            LoadEntries();
        }
    }

    // ── Plant location preview ────────────────────────────────────────────────

    public GardenArea? SelectedPlantLocationArea =>
        _selectedPlant == null ? null :
        Areas.FirstOrDefault(a => a.PlantPlacements.Any(p => p.PlantId == _selectedPlant.Id));

    public bool SelectedPlantHasLocation => SelectedPlantLocationArea != null;

    public string SelectedPlantLocationLabel =>
        SelectedPlantLocationArea == null ? "" : $"Located in: {SelectedPlantLocationArea.Name}";

    public DiaryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set { _selectedEntry = value; OnPropertyChanged(); }
    }

    public string SelectedPlantTitle =>
        _selectedPlant is null ? "Select a plant to view diary" : $"{_selectedPlant.CommonName} — Diary";

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    public DateTime? SelectedCalendarDate
    {
        get => _selectedCalendarDate;
        set
        {
            _selectedCalendarDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DayTasksTitle));
            OnPropertyChanged(nameof(NoDayTasks));
            OnPropertyChanged(nameof(NoDayAreaTasks));
            OnPropertyChanged(nameof(NoDayContent));
            LoadDayTasks();
            _ = LoadWeatherAsync();
        }
    }

    public string DayTasksTitle => _selectedCalendarDate.HasValue
        ? _selectedCalendarDate.Value.ToString("dddd, MMMM d, yyyy")
        : "Select a day on the calendar";

    public bool NoDayTasks    => DayTasks.Count == 0;
    public bool NoDayAreaTasks => DayAreaTasks.Count == 0;
    public bool NoDayContent  => DayTasks.Count == 0 && DayAreaTasks.Count == 0;

    public bool ShowCompletedTasks
    {
        get => _showCompletedTasks;
        set { if (_showCompletedTasks == value) return; _showCompletedTasks = value; OnPropertyChanged(); RebuildTaskCards(); }
    }
    public bool HasNoActiveTasks      => TaskCards.Count == 0;
    public bool HasCompletedTasks     => CompletedTaskCards.Count > 0;
    public int  CompletedTaskCount    => CompletedTaskCards.Count;

    // ── Weather ───────────────────────────────────────────────────────────────

    public DayWeather? DayWeather
    {
        get => _dayWeather;
        private set
        {
            _dayWeather = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWeather));
            OnPropertyChanged(nameof(ShowWeatherNoLocation));
            OnPropertyChanged(nameof(ShowWeatherLoading));
            OnPropertyChanged(nameof(ShowWeatherError));
            OnPropertyChanged(nameof(WeatherCondition));
            OnPropertyChanged(nameof(WeatherTempRange));
            OnPropertyChanged(nameof(WeatherWind));
            OnPropertyChanged(nameof(WeatherPrecipitation));
            OnPropertyChanged(nameof(WeatherSunrise));
            OnPropertyChanged(nameof(WeatherSunset));
            OnPropertyChanged(nameof(WeatherErrorText));
        }
    }

    public bool IsLoadingWeather
    {
        get => _isLoadingWeather;
        private set
        {
            _isLoadingWeather = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowWeatherLoading));
            OnPropertyChanged(nameof(ShowWeatherError));
        }
    }

    public bool HasHomeLocation => _backupService.Settings.HomeLatitude.HasValue;
    public bool HasWeather      => _dayWeather?.IsAvailable == true;

    // Visibility helpers (mutually exclusive states for the weather panel)
    public bool ShowWeatherNoLocation => !HasHomeLocation;
    public bool ShowWeatherLoading    => HasHomeLocation && IsLoadingWeather;
    public bool ShowWeatherError      => HasHomeLocation && !IsLoadingWeather && !HasWeather
                                         && _selectedCalendarDate.HasValue;

    // Formatted weather strings
    public string WeatherCondition    => HasWeather ? _dayWeather!.Condition : "";
    public string WeatherTempRange    => HasWeather ? $"{_dayWeather!.TempMin:F0}–{_dayWeather.TempMax:F0}°C" : "";
    public string WeatherWind         => HasWeather
        ? $"{_dayWeather!.WindSpeed:F0} km/h {WeatherService.DegreesToCompass(_dayWeather.WindDirection)}" : "";
    public string WeatherPrecipitation => HasWeather ? $"{_dayWeather!.Precipitation:F1} mm" : "";
    public string WeatherSunrise      => HasWeather ? _dayWeather!.Sunrise.ToString("HH:mm") : "";
    public string WeatherSunset       => HasWeather ? _dayWeather!.Sunset.ToString("HH:mm") : "";
    public string WeatherErrorText    => ShowWeatherError ? (_dayWeather?.Error ?? "Weather data unavailable.") : "";

    // ── Garden Planner ────────────────────────────────────────────────────────

    public GardenArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            _selectedArea = value;
            SelectedPlacement = null;
            SelectedShape = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAreaTitle));
            OnPropertyChanged(nameof(HasSelectedArea));
            _backupService.Settings.LastSelectedAreaId = value?.Id;
            _backupService.SaveSettings();
            CanvasRefreshRequested?.Invoke();
        }
    }

    public string SelectedAreaTitle => _selectedArea?.Name ?? "Select an area";

    public bool HasSelectedArea => _selectedArea != null;

    public PlantOption? SelectedPlantOption
    {
        get => _selectedPlantOption;
        set
        {
            _selectedPlantOption = value;
            PlantToPlace = value?.Plant;
            OnPropertyChanged();
        }
    }

    public Plant? PlantToPlace
    {
        get => _plantToPlace;
        set
        {
            _plantToPlace = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DefaultPlacementRadius));
        }
    }

    public double DefaultPlacementRadius
    {
        get => _defaultRadius;
        set
        {
            if (value <= 0) return;
            _defaultRadius = value;
            OnPropertyChanged();
        }
    }

    public PlantPlacement? SelectedPlacement
    {
        get => _selectedPlacement;
        set
        {
            _selectedPlacement = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPlacementRadius));
            OnPropertyChanged(nameof(SelectedPlacementLabel));
            OnPropertyChanged(nameof(HasSelectedPlacement));
        }
    }

    public double SelectedPlacementRadius
    {
        get => _selectedPlacement?.Radius ?? 30;
        set
        {
            if (_selectedPlacement == null || value <= 0) return;
            _selectedPlacement.Radius = value;
            OnPropertyChanged();
            SaveAreas();
            PlacementResized?.Invoke(_selectedPlacement.Id, value);
        }
    }

    public string SelectedPlacementLabel
    {
        get
        {
            if (_selectedPlacement == null) return "Click a plant on the canvas to select it";
            var plant = Plants.FirstOrDefault(p => p.Id == _selectedPlacement.PlantId);
            return plant is null ? "Unknown plant" : $"{plant.CommonName}  —  drag to move";
        }
    }

    public bool HasSelectedPlacement => _selectedPlacement != null;

    // ── Shapes ──────────────────────────────────────────────────────────────

    public GardenShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            _selectedShape = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedShape));
            OnPropertyChanged(nameof(SelectedShapeLabel));
        }
    }

    public bool HasSelectedShape => _selectedShape != null;

    public string SelectedShapeLabel => _selectedShape == null
        ? "" : $"{_selectedShape.Type}  {_selectedShape.Width:F0}×{_selectedShape.Height:F0} cm";

    // Raised when the canvas must be redrawn
    public event Action? CanvasRefreshRequested;

    // Raised when only one placement's radius changed — avoids full canvas rebuild
    public event Action<Guid, double>? PlacementResized;

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand AddPlantCommand { get; }
    public RelayCommand EditPlantCommand { get; }
    public RelayCommand DuplicatePlantCommand { get; }
    public RelayCommand DeletePlantCommand { get; }
    public RelayCommand AddEntryCommand { get; }
    public RelayCommand EditEntryCommand { get; }
    public RelayCommand DeleteEntryCommand { get; }
    public RelayCommand BackupNowCommand { get; }
    public RelayCommand ConfigureBackupCommand { get; }
    public RelayCommand CalendarAddEntryCommand { get; }
    public RelayCommand EditCalendarEntryCommand { get; }
    public RelayCommand AddAreaCommand { get; }
    public RelayCommand EditAreaCommand { get; }
    public RelayCommand DeleteAreaCommand { get; }
    public RelayCommand DeletePlacementCommand { get; }
    public RelayCommand RestoreBackupCommand { get; }
    public RelayCommand SetHomeLocationCommand { get; }
    public RelayCommand AddRectangleCommand { get; }
    public RelayCommand AddCircleCommand { get; }
    public RelayCommand DeleteShapeCommand { get; }
    public RelayCommand BringShapeToFrontCommand { get; }
    public RelayCommand SendShapeToBackCommand { get; }
    public RelayCommand CalendarAddAreaEntryCommand  { get; }
    public RelayCommand EditCalendarAreaEntryCommand { get; }
    public RelayCommand AddTaskCommand       { get; }
    public RelayCommand EditTaskCommand      { get; }
    public RelayCommand DeleteTaskCommand    { get; }
    public RelayCommand SkipTaskCommand      { get; }
    public RelayCommand ToggleCompletedCommand { get; }
    public RelayCommand ChangeDataFolderCommand { get; }

    public string DataFolderPath => _dataService.AppDataDir;

    public MainViewModel()
    {
        _backupService = new BackupService(_dataService.AppDataDir, _dataService.DataFilePath, _dataService.AreasFilePath);

        AddPlantCommand         = new RelayCommand(_ => AddPlant());
        EditPlantCommand        = new RelayCommand(_ => EditPlant(),        _ => SelectedPlant != null);
        DuplicatePlantCommand   = new RelayCommand(_ => DuplicatePlant(),   _ => SelectedPlant != null);
        DeletePlantCommand      = new RelayCommand(_ => DeletePlant(),      _ => SelectedPlant != null);
        AddEntryCommand         = new RelayCommand(_ => AddEntry(),    _ => SelectedPlant != null);
        EditEntryCommand        = new RelayCommand(_ => EditEntry(),   _ => SelectedEntry != null);
        DeleteEntryCommand      = new RelayCommand(_ => DeleteEntry(), _ => SelectedEntry != null);
        BackupNowCommand        = new RelayCommand(_ => BackupNow());
        ConfigureBackupCommand  = new RelayCommand(_ => ConfigureBackup());
        CalendarAddEntryCommand  = new RelayCommand(_ => CalendarAddEntry(), _ => SelectedCalendarDate.HasValue);
        EditCalendarEntryCommand = new RelayCommand(obj => { if (obj is string name) EditActivityEntry(name); });
        AddAreaCommand          = new RelayCommand(_ => AddArea());
        EditAreaCommand         = new RelayCommand(_ => EditArea(),    _ => SelectedArea != null);
        DeleteAreaCommand       = new RelayCommand(_ => DeleteArea(),  _ => SelectedArea != null);
        DeletePlacementCommand  = new RelayCommand(_ => DeletePlacement(), _ => SelectedPlacement != null);
        RestoreBackupCommand    = new RelayCommand(_ => RestoreBackup());
        SetHomeLocationCommand  = new RelayCommand(_ => SetHomeLocation());
        AddRectangleCommand     = new RelayCommand(_ => AddShape(ShapeType.Rectangle), _ => SelectedArea != null);
        AddCircleCommand        = new RelayCommand(_ => AddShape(ShapeType.Circle),    _ => SelectedArea != null);
        DeleteShapeCommand      = new RelayCommand(_ => DeleteShape(),       _ => SelectedShape != null);
        BringShapeToFrontCommand = new RelayCommand(_ => BringShapeToFront(), _ => SelectedShape != null);
        SendShapeToBackCommand  = new RelayCommand(_ => SendShapeToBack(),   _ => SelectedShape != null);
        CalendarAddAreaEntryCommand  = new RelayCommand(_ => CalendarAddAreaEntry(), _ => SelectedCalendarDate.HasValue);
        EditCalendarAreaEntryCommand = new RelayCommand(obj => { if (obj is Guid id) EditSingleAreaDay(id); });
        AddTaskCommand       = new RelayCommand(_ => AddTask());
        EditTaskCommand      = new RelayCommand(obj => { if (obj is GardenTask t) EditTask(t); });
        DeleteTaskCommand    = new RelayCommand(obj => { if (obj is GardenTask t) DeleteTask(t); });
        SkipTaskCommand      = new RelayCommand(obj => { if (obj is GardenTask t) SkipTask(t); });
        ToggleCompletedCommand      = new RelayCommand(_ => ShowCompletedTasks = !ShowCompletedTasks);
        ChangeDataFolderCommand     = new RelayCommand(_ => ChangeDataFolder());

        LoadPlants();
        LoadAreas();
        LoadTasksData();
        RebuildReminders();
    }

    // ── Auto-backup ───────────────────────────────────────────────────────────

    public void RunAutoBackupIfDue()
    {
        if (!_backupService.ShouldAutoBackup()) return;
        try
        {
            var dest = _backupService.Backup(isAuto: true);
            StatusText = $"Auto-backup created: {dest}";
        }
        catch (Exception ex)
        {
            StatusText = $"Auto-backup skipped: {ex.Message}";
        }
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private void LoadPlants()
    {
        Plants.Clear();
        foreach (var p in _dataService.LoadPlants())
            Plants.Add(p);
        RefreshPlantOptions();
    }

    private void LoadAreas()
    {
        Areas.Clear();
        foreach (var a in _dataService.LoadAreas())
            Areas.Add(a);
        RefreshPlantOptions();

        var lastId = _backupService.Settings.LastSelectedAreaId;
        _selectedArea = lastId.HasValue
            ? Areas.FirstOrDefault(a => a.Id == lastId) ?? Areas.FirstOrDefault()
            : Areas.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedArea));
        OnPropertyChanged(nameof(SelectedAreaTitle));
        OnPropertyChanged(nameof(HasSelectedArea));
    }

    private void RefreshPlantOptions()
    {
        // Map plantId -> area name for all placed plants
        var placed = new Dictionary<Guid, string>();
        foreach (var area in Areas)
            foreach (var pp in area.PlantPlacements)
                placed.TryAdd(pp.PlantId, area.Name);

        // Remove options for plants that no longer exist
        var existingPlantIds = Plants.Select(p => p.Id).ToHashSet();
        for (int i = PlantOptions.Count - 1; i >= 0; i--)
            if (!existingPlantIds.Contains(PlantOptions[i].Plant.Id))
                PlantOptions.RemoveAt(i);

        // Add options for new plants
        var optionIds = PlantOptions.Select(o => o.Plant.Id).ToHashSet();
        foreach (var plant in Plants)
            if (!optionIds.Contains(plant.Id))
                PlantOptions.Add(new PlantOption(plant));

        // Update plant reference (handles renames) and availability for all options
        var plantById = Plants.ToDictionary(p => p.Id);
        foreach (var opt in PlantOptions)
        {
            if (plantById.TryGetValue(opt.Plant.Id, out var current) && !ReferenceEquals(current, opt.Plant))
                opt.Plant = current;

            if (placed.TryGetValue(opt.Plant.Id, out var areaName))
            {
                opt.IsAvailable = false;
                opt.PlacedAreaName = areaName;
            }
            else
            {
                opt.IsAvailable = true;
                opt.PlacedAreaName = "";
            }
        }

        OnPropertyChanged(nameof(UnplacedPlantOptions));
        RefreshPlantFilters();
        RefreshFilteredPlants();

        // Only notify when selected plant's location has actually changed  (#8)
        if (_selectedPlant != null)
        {
            var newAreaName = placed.TryGetValue(_selectedPlant.Id, out var an) ? an : null;
            if (newAreaName != SelectedPlantLocationArea?.Name)
            {
                OnPropertyChanged(nameof(SelectedPlantLocationArea));
                OnPropertyChanged(nameof(SelectedPlantHasLocation));
                OnPropertyChanged(nameof(SelectedPlantLocationLabel));
            }
        }
    }

    private void RefreshPlantFilters()
    {
        var prevAreaId = _selectedPlantFilter?.AreaId;
        PlantFilterItems.Clear();
        PlantFilterItems.Add(new PlantFilterItem("All Plants", null));
        PlantFilterItems.Add(new PlantFilterItem("Not Placed", Guid.Empty));
        foreach (var area in Areas)
            PlantFilterItems.Add(new PlantFilterItem(area.Name, area.Id));

        _selectedPlantFilter = PlantFilterItems.FirstOrDefault(f => f.AreaId == prevAreaId)
                               ?? PlantFilterItems[0];
        OnPropertyChanged(nameof(SelectedPlantFilter));
    }

    private void RefreshFilteredPlants()
    {
        FilteredPlants.Clear();
        var filter = _selectedPlantFilter;

        IEnumerable<Plant> subset;
        if (filter == null || filter.AreaId == null)
        {
            subset = Plants;
        }
        else if (filter.AreaId == Guid.Empty)
        {
            var placed = Areas.SelectMany(a => a.PlantPlacements).Select(p => p.PlantId).ToHashSet();
            subset = Plants.Where(p => !placed.Contains(p.Id));
        }
        else
        {
            var area = Areas.FirstOrDefault(a => a.Id == filter.AreaId);
            var areaPlantIds = area?.PlantPlacements.Select(p => p.PlantId).ToHashSet() ?? [];
            subset = Plants.Where(p => areaPlantIds.Contains(p.Id));
        }

        var q = _plantSearchText.Trim();
        if (!string.IsNullOrEmpty(q))
            subset = subset.Where(p =>
                p.CommonName.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                (p.LatinName ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                (p.Variety   ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase));

        foreach (var p in subset.OrderBy(p => p.CommonName, StringComparer.CurrentCultureIgnoreCase))
            FilteredPlants.Add(p);
    }

    private void LoadEntries()
    {
        Entries.Clear();
        if (_selectedPlant == null) return;
        foreach (var e in _selectedPlant.DiaryEntries.OrderByDescending(e => e.Date))
            Entries.Add(e);
    }

    internal static readonly (string Name, Func<DiaryEntry, bool> Selector, string Bg, string Fg)[] ActivityDefs =
    {
        ("Watering",              e => e.Watering,                 "#BBDEFB", "#0D47A1"),
        ("Fertilizing & Mulching",e => e.Fertilizing || e.Mulching,"#FFE0B2", "#BF360C"),
        ("Pruning",               e => e.Pruning,                  "#E1BEE7", "#4A148C"),
        ("Planting",              e => e.Planting,                 "#C8E6C9", "#1B5E20"),
    };

    internal static readonly (string Name, Func<AreaDiaryEntry, bool> Selector, string Bg, string Fg)[] AreaActivityDefs =
    {
        ("Mowing",      e => e.Mowing,      "#DCEDC8", "#33691E"),
        ("Watering",    e => e.Watering,    "#B3E5FC", "#01579B"),
        ("Overseeding", e => e.Overseeding, "#F9FBE7", "#827717"),
        ("Feeding",     e => e.Feeding,     "#FFE0B2", "#E65100"),
        ("Aerating",    e => e.Aerating,    "#D7CCC8", "#4E342E"),
    };

    private void LoadDayTasks()
    {
        DayTasks.Clear();
        DayAreaTasks.Clear();
        if (!_selectedCalendarDate.HasValue) return;

        var date = _selectedCalendarDate.Value.Date;

        // Pre-build plant→area map once for all activity loops  (#6)
        var plantAreaName = new Dictionary<Guid, string>();
        foreach (var area in Areas)
            foreach (var pp in area.PlantPlacements)
                plantAreaName.TryAdd(pp.PlantId, area.Name);

        // Lawn areas first (Feature 1)
        foreach (var area in Areas)
        {
            var entry = area.DiaryEntries.FirstOrDefault(e => e.Date.Date == date);
            if (entry == null) continue;
            var acts = AreaActivityDefs
                .Where(d => d.Selector(entry))
                .Select(d => d.Name)
                .ToList();
            if (acts.Count == 0) continue;
            DayAreaTasks.Add(new AreaSummary
            {
                AreaId     = area.Id,
                Name       = area.Name,
                Activities = string.Join(", ", acts),
                Notes      = entry.Notes ?? ""
            });
        }

        foreach (var (name, selector, bg, fg) in ActivityDefs)
        {
            var plants = Plants
                .Where(p => p.DiaryEntries.Any(e => e.Date.Date == date && selector(e)))
                .Select(p =>
                {
                    var entry    = p.DiaryEntries.First(e => e.Date.Date == date && selector(e));
                    var areaName = plantAreaName.TryGetValue(p.Id, out var an) ? an : "";
                    return new PlantSummary
                    {
                        PlantId          = p.Id,
                        Name             = FormatPlantDisplayName(p.CommonName, p.LatinName, p.Variety),
                        LatinName        = "",
                        Notes            = entry.Notes ?? "",
                        LocationAreaName = areaName
                    };
                })
                .ToList();

            if (plants.Count > 0)
                DayTasks.Add(new DayTaskGroup
                {
                    ActivityName    = name,
                    BadgeBackground = bg,
                    BadgeForeground = fg,
                    Plants          = plants
                });
        }

        OnPropertyChanged(nameof(NoDayTasks));
        OnPropertyChanged(nameof(NoDayAreaTasks));
        OnPropertyChanged(nameof(NoDayContent));
        RebuildDaysWithActivities();
    }

    private void RebuildDaysWithActivities()
    {
        var newDays = new HashSet<DateTime>();
        foreach (var plant in Plants)
            foreach (var entry in plant.DiaryEntries)
                if (entry.Planting || entry.Watering || entry.Fertilizing ||
                    entry.Weeding  || entry.Mulching  || entry.Pruning)
                    newDays.Add(entry.Date.Date);

        foreach (var area in Areas)
            foreach (var entry in area.DiaryEntries)
                if (entry.Mowing || entry.Watering || entry.Overseeding || entry.Feeding || entry.Aerating)
                    newDays.Add(entry.Date.Date);

        if (newDays.SetEquals(DaysWithActivities)) return;

        DaysWithActivities.Clear();
        foreach (var d in newDays) DaysWithActivities.Add(d);
        OnPropertyChanged(nameof(DaysWithActivities));
    }

    // Save plant data only — use when diary entries have NOT changed (e.g. plant metadata edits)
    private void Save() => _dataService.SavePlants(Plants);

    // Save plant data and refresh the calendar view — use when diary entries have changed
    private void SaveWithCalendarRefresh()
    {
        _dataService.SavePlants(Plants);
        CheckTaskCompletion();
        SaveTasksData();
        RebuildTaskCards();
        RebuildReminders();
        LoadDayTasks();
    }

    public void SaveAreas() => _dataService.SaveAreas(Areas);

    // ── Plant CRUD ────────────────────────────────────────────────────────────

    private void AddPlant()
    {
        var dialog = new PlantEditDialog(new Plant()) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            Plants.Add(dialog.Plant);
            Save();
            RefreshPlantOptions();
            SelectedPlant = dialog.Plant;
        }
    }

    private void EditPlant()
    {
        if (SelectedPlant == null) return;
        var copy = SelectedPlant.Clone();
        var dialog = new PlantEditDialog(copy) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            var idx = Plants.IndexOf(SelectedPlant);
            Plants[idx] = dialog.Plant;
            Save();
            RefreshPlantOptions();
            SelectedPlant = Plants[idx];
        }
    }

    private void DuplicatePlant()
    {
        if (SelectedPlant == null) return;
        var copy = SelectedPlant.Clone();
        copy.Id         = Guid.NewGuid();
        copy.CommonName = NextDuplicateName(SelectedPlant.CommonName, Plants.Select(p => p.CommonName));
        copy.DiaryEntries = new List<DiaryEntry>();
        Plants.Add(copy);
        Save();
        RefreshPlantOptions();
        SelectedPlant = copy;
    }

    /// <summary>
    /// "Tree" → "Tree 2", "Tree 2" → "Tree 3", skipping names that already exist.
    /// </summary>
    private static string NextDuplicateName(string sourceName, IEnumerable<string> existingNames)
    {
        var match = Regex.Match(sourceName, @"^(.*?)\s+(\d+)$");
        string basePart;
        int    nextNum;
        if (match.Success)
        {
            basePart = match.Groups[1].Value;
            nextNum  = int.Parse(match.Groups[2].Value) + 1;
        }
        else
        {
            basePart = sourceName;
            nextNum  = 2;
        }

        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string candidate;
        do { candidate = $"{basePart} {nextNum++}"; } while (existing.Contains(candidate));
        return candidate;
    }

    private void DeletePlant()
    {
        if (SelectedPlant == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete '{SelectedPlant.CommonName}' and all its diary entries and placements?",
            "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        // Remove placements from all areas
        var plantId = SelectedPlant.Id;
        foreach (var area in Areas)
            area.PlantPlacements.RemoveAll(p => p.PlantId == plantId);
        SaveAreas();

        Plants.Remove(SelectedPlant);
        SelectedPlant = null;
        SaveWithCalendarRefresh();
        RefreshPlantOptions();
        CanvasRefreshRequested?.Invoke();
    }

    // ── Diary entry CRUD ──────────────────────────────────────────────────────

    private void AddEntry()
    {
        if (SelectedPlant == null) return;
        var dialog = new DiaryEntryEditDialog(new DiaryEntry()) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            SelectedPlant.DiaryEntries.Add(dialog.Entry);
            LoadEntries();
            SaveWithCalendarRefresh();
        }
    }

    private void EditEntry()
    {
        if (SelectedPlant == null || SelectedEntry == null) return;
        var copy = new DiaryEntry
        {
            Id          = SelectedEntry.Id,
            Date        = SelectedEntry.Date,
            Planting    = SelectedEntry.Planting,
            Watering    = SelectedEntry.Watering,
            Fertilizing = SelectedEntry.Fertilizing,
            Weeding     = SelectedEntry.Weeding,
            Mulching    = SelectedEntry.Mulching,
            Pruning     = SelectedEntry.Pruning,
            Notes       = SelectedEntry.Notes
        };
        var dialog = new DiaryEntryEditDialog(copy) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            var idx = SelectedPlant.DiaryEntries.IndexOf(SelectedEntry);
            SelectedPlant.DiaryEntries[idx] = dialog.Entry;
            LoadEntries();
            SaveWithCalendarRefresh();
        }
    }

    private void DeleteEntry()
    {
        if (SelectedPlant == null || SelectedEntry == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete diary entry for {SelectedEntry.Date:yyyy-MM-dd}?",
            "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        SelectedPlant.DiaryEntries.Remove(SelectedEntry);
        LoadEntries();
        SaveWithCalendarRefresh();
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    private void CalendarAddEntry()
    {
        if (!_selectedCalendarDate.HasValue) return;
        var dialog = new CalendarEntryDialog(_selectedCalendarDate.Value, Plants, Areas)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveCalendarEntry(dialog.SelectedPlants, dialog.Entry);
    }

    private void EditActivityEntry(string activityName)
    {
        if (!_selectedCalendarDate.HasValue) return;
        var date = _selectedCalendarDate.Value.Date;

        var def = ActivityDefs.FirstOrDefault(d => d.Name == activityName);
        if (def == default) return;

        var checkedIds = Plants
            .Where(p => p.DiaryEntries.Any(e => e.Date.Date == date && def.Selector(e)))
            .Select(p => p.Id)
            .ToHashSet();

        var dialog = new EditActivityDialog(date, activityName, Plants, Areas, checkedIds)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        foreach (var item in dialog.PlantResults)
        {
            var plant = Plants.FirstOrDefault(p => p.Id == item.PlantId);
            if (plant == null) continue;

            var entry = plant.DiaryEntries.FirstOrDefault(e => e.Date.Date == date);

            if (item.IsChecked)
            {
                if (entry == null)
                {
                    entry = new DiaryEntry { Date = date };
                    plant.DiaryEntries.Add(entry);
                }
                SetActivityFlag(entry, activityName, true);
            }
            else if (entry != null)
            {
                SetActivityFlag(entry, activityName, false);
                if (!entry.Planting && !entry.Watering && !entry.Fertilizing &&
                    !entry.Weeding  && !entry.Mulching  && !entry.Pruning)
                    plant.DiaryEntries.Remove(entry);
            }
        }
        SaveWithCalendarRefresh();
    }

    private static void SetActivityFlag(DiaryEntry entry, string activityName, bool value)
    {
        switch (activityName)
        {
            case "Planting":               entry.Planting    = value; break;
            case "Watering":               entry.Watering    = value; break;
            case "Fertilizing & Mulching": entry.Fertilizing = value; entry.Mulching = value; break;
            case "Pruning":                entry.Pruning     = value; break;
        }
    }

    // ── Lawn (Area) Calendar ─────────────────────────────────────────────────

    private void CalendarAddAreaEntry()
    {
        if (!_selectedCalendarDate.HasValue) return;
        var dialog = new AreaCalendarEntryDialog(_selectedCalendarDate.Value, Areas)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveAreaCalendarEntry(dialog.SelectedAreas, dialog.Entry);
    }

    private void SaveAreaCalendarEntry(List<GardenArea> areas, AreaDiaryEntry newEntry)
    {
        foreach (var area in areas)
        {
            var existing = area.DiaryEntries.FirstOrDefault(e => e.Date.Date == newEntry.Date.Date);
            if (existing != null)
            {
                if (newEntry.Mowing)      existing.Mowing      = true;
                if (newEntry.Watering)    existing.Watering    = true;
                if (newEntry.Overseeding) existing.Overseeding = true;
                if (newEntry.Feeding)     existing.Feeding     = true;
                if (newEntry.Aerating)    existing.Aerating    = true;
                if (!string.IsNullOrWhiteSpace(newEntry.Notes)) existing.Notes = newEntry.Notes;
            }
            else
            {
                area.DiaryEntries.Add(new AreaDiaryEntry
                {
                    Date        = newEntry.Date,
                    Mowing      = newEntry.Mowing,
                    Watering    = newEntry.Watering,
                    Overseeding = newEntry.Overseeding,
                    Feeding     = newEntry.Feeding,
                    Aerating    = newEntry.Aerating,
                    Notes       = newEntry.Notes
                });
            }
        }
        SaveAreas();
        CheckTaskCompletion();
        SaveTasksData();
        RebuildTaskCards();
        RebuildReminders();
        LoadDayTasks();
    }

    private void EditSingleAreaDay(Guid areaId)
    {
        if (!_selectedCalendarDate.HasValue) return;
        var date = _selectedCalendarDate.Value.Date;
        var area = Areas.FirstOrDefault(a => a.Id == areaId);
        if (area == null) return;

        var existing = area.DiaryEntries.FirstOrDefault(e => e.Date.Date == date);
        var dialog = new AreaCalendarEntryDialog(date, area, existing, Areas)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        var entry = dialog.Entry;
        if (existing != null)
        {
            existing.Mowing      = entry.Mowing;
            existing.Watering    = entry.Watering;
            existing.Overseeding = entry.Overseeding;
            existing.Feeding     = entry.Feeding;
            existing.Aerating    = entry.Aerating;
            existing.Notes       = entry.Notes;
            if (!existing.Mowing && !existing.Watering && !existing.Overseeding &&
                !existing.Feeding && !existing.Aerating)
                area.DiaryEntries.Remove(existing);
        }
        else
        {
            entry.Date = date;
            area.DiaryEntries.Add(entry);
        }
        SaveAreas();
        CheckTaskCompletion();
        SaveTasksData();
        RebuildTaskCards();
        RebuildReminders();
        LoadDayTasks();
    }

    // Called from garden planner double-click → navigate to Plants & Diary tab
    public void NavigateToPlant(Guid plantId)
    {
        // Reset filters so the plant is visible
        PlantSearchText = "";
        var allFilter = PlantFilterItems.FirstOrDefault(f => f.AreaId == null);
        if (allFilter != null) SelectedPlantFilter = allFilter;

        var plant = Plants.FirstOrDefault(p => p.Id == plantId);
        if (plant != null) SelectedPlant = plant;
    }

    // Called from garden planner double-click
    public void OpenActivityDialogForPlacement(Guid plantId)
    {
        var plant = Plants.FirstOrDefault(p => p.Id == plantId);
        if (plant == null) return;
        var dialog = new CalendarEntryDialog(DateTime.Today, Plants, Areas, plant)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveCalendarEntry(dialog.SelectedPlants, dialog.Entry);
    }

    private void SaveCalendarEntry(List<Plant> plants, DiaryEntry newEntry)
    {
        foreach (var plant in plants)
        {
            var existing = plant.DiaryEntries.FirstOrDefault(e => e.Date.Date == newEntry.Date.Date);
            if (existing != null)
            {
                existing.Planting    = newEntry.Planting;
                existing.Watering    = newEntry.Watering;
                existing.Fertilizing = newEntry.Fertilizing;
                existing.Weeding     = newEntry.Weeding;
                existing.Mulching    = newEntry.Mulching;
                existing.Pruning     = newEntry.Pruning;
                existing.Notes       = newEntry.Notes;
            }
            else
            {
                plant.DiaryEntries.Add(new DiaryEntry
                {
                    Date        = newEntry.Date,
                    Planting    = newEntry.Planting,
                    Watering    = newEntry.Watering,
                    Fertilizing = newEntry.Fertilizing,
                    Weeding     = newEntry.Weeding,
                    Mulching    = newEntry.Mulching,
                    Pruning     = newEntry.Pruning,
                    Notes       = newEntry.Notes
                });
            }
            if (SelectedPlant?.Id == plant.Id) LoadEntries();
        }
        SaveWithCalendarRefresh();
    }

    // ── Garden Planner ────────────────────────────────────────────────────────

    private void AddArea()
    {
        var dialog = new GardenAreaEditDialog(new GardenArea()) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            Areas.Add(dialog.Area);
            SaveAreas();
            SelectedArea = dialog.Area;
        }
    }

    private void EditArea()
    {
        if (SelectedArea == null) return;
        var copy = SelectedArea.Clone();
        var dialog = new GardenAreaEditDialog(copy) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            var idx = Areas.IndexOf(SelectedArea);
            Areas[idx] = dialog.Area;
            SaveAreas();
            SelectedArea = Areas[idx];
        }
    }

    private void DeleteArea()
    {
        if (SelectedArea == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete area '{SelectedArea.Name}' and all its plant placements?",
            "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        Areas.Remove(SelectedArea);
        SelectedArea = null;
        SaveAreas();
        RefreshPlantOptions();
    }

    private void DeletePlacement()
    {
        if (SelectedArea == null || SelectedPlacement == null) return;
        SelectedArea.PlantPlacements.Remove(SelectedPlacement);
        SelectedPlacement = null;
        SaveAreas();
        RefreshPlantOptions();
        CanvasRefreshRequested?.Invoke();
    }

    public PlantPlacement? AddPlacement(double x, double y)
    {
        if (SelectedArea == null || PlantToPlace == null) return null;

        // Each plant can only be placed once across all areas
        var alreadyPlaced = Areas.Any(a => a.PlantPlacements.Any(p => p.PlantId == PlantToPlace.Id));
        if (alreadyPlaced) return null;

        var placement = new PlantPlacement
        {
            PlantId = PlantToPlace.Id,
            X       = x,
            Y       = y,
            Radius  = PlantToPlace.DefaultRadius > 0 ? PlantToPlace.DefaultRadius : DefaultPlacementRadius
        };
        SelectedArea.PlantPlacements.Add(placement);
        SaveAreas();
        RefreshPlantOptions();
        return placement;
    }

    // ── Shapes ──────────────────────────────────────────────────────────────

    private void AddShape(ShapeType type)
    {
        if (SelectedArea == null) return;
        var maxZ = SelectedArea.Shapes.Count > 0
            ? SelectedArea.Shapes.Max(s => s.ZIndex) + 1 : 0;
        var shape = new GardenShape
        {
            Type   = type,
            X      = Math.Min(100, SelectedArea.Width  / 2),
            Y      = Math.Min(100, SelectedArea.Height / 2),
            Width  = type == ShapeType.Circle ? 80 : 100,
            Height = type == ShapeType.Circle ? 80 : 60,
            ZIndex = maxZ
        };
        SelectedArea.Shapes.Add(shape);
        SaveAreas();
        SelectedShape = shape;
        CanvasRefreshRequested?.Invoke();
    }

    private void DeleteShape()
    {
        if (SelectedArea == null || SelectedShape == null) return;
        SelectedArea.Shapes.Remove(SelectedShape);
        SelectedShape = null;
        SaveAreas();
        CanvasRefreshRequested?.Invoke();
    }

    private void BringShapeToFront()
    {
        if (SelectedArea == null || SelectedShape == null) return;
        var maxZ = SelectedArea.Shapes.Max(s => s.ZIndex);
        if (SelectedShape.ZIndex >= maxZ) return;
        SelectedShape.ZIndex = maxZ + 1;
        SaveAreas();
        CanvasRefreshRequested?.Invoke();
    }

    private void SendShapeToBack()
    {
        if (SelectedArea == null || SelectedShape == null) return;
        var minZ = SelectedArea.Shapes.Min(s => s.ZIndex);
        if (SelectedShape.ZIndex <= minZ) return;
        SelectedShape.ZIndex = minZ - 1;
        SaveAreas();
        CanvasRefreshRequested?.Invoke();
    }

    // ── Weather ───────────────────────────────────────────────────────────────

    private async Task LoadWeatherAsync()
    {
        var settings = _backupService.Settings;
        if (!settings.HomeLatitude.HasValue || !_selectedCalendarDate.HasValue)
        {
            DayWeather = null;
            return;
        }

        IsLoadingWeather = true;
        DayWeather = null;
        try
        {
            DayWeather = await _weatherService.GetWeatherAsync(
                settings.HomeLatitude.Value,
                settings.HomeLongitude!.Value,
                DateOnly.FromDateTime(_selectedCalendarDate.Value));
        }
        catch (Exception ex)
        {
            DayWeather = new DayWeather { IsAvailable = false, Error = ex.Message };
        }
        finally
        {
            IsLoadingWeather = false;
        }
    }

    private void SetHomeLocation()
    {
        var settings = _backupService.Settings;
        var dialog = new LocationPickerDialog(settings.HomeLatitude, settings.HomeLongitude)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        settings.HomeLatitude  = dialog.Latitude;
        settings.HomeLongitude = dialog.Longitude;
        _backupService.SaveSettings();

        OnPropertyChanged(nameof(HasHomeLocation));
        OnPropertyChanged(nameof(ShowWeatherNoLocation));
        _ = LoadWeatherAsync();
    }

    // ── Backup / Restore ──────────────────────────────────────────────────────

    private void RestoreBackup()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title            = "Select a Backup File to Restore (data or areas file)",
            Filter           = "JSON backup files|GardenDiary_*.json|All JSON files|*.json",
            InitialDirectory = Directory.Exists(_backupService.Settings.BackupFolderPath)
                                   ? _backupService.Settings.BackupFolderPath
                                   : null
        };
        if (dlg.ShowDialog() != true) return;

        // Accept either the data file or the areas file — derive the other automatically
        string dataPath, areasPath;
        if (BackupService.IsAreasBackupFile(dlg.FileName))
        {
            areasPath = dlg.FileName;
            dataPath  = BackupService.GetDataBackupPath(dlg.FileName);
        }
        else
        {
            dataPath  = dlg.FileName;
            areasPath = BackupService.GetAreasBackupPath(dlg.FileName);
        }

        var missing = !File.Exists(dataPath)  ? $"Data file:\n  {dataPath}"
                    : !File.Exists(areasPath) ? $"Areas file:\n  {areasPath}"
                    : null;
        if (missing != null)
        {
            System.Windows.MessageBox.Show(
                $"Cannot restore — the corresponding backup file was not found:\n\n{missing}\n\n" +
                "Both files must be present in the same folder.",
                "Restore Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Restore from:\n  • {Path.GetFileName(dataPath)}\n  • {Path.GetFileName(areasPath)}\n\n" +
            "A safety backup of the current data will be created first. Continue?",
            "Restore Backup", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try { _backupService.SafetyBackup(); }
        catch (Exception ex) { StatusText = $"Safety backup warning: {ex.Message}"; }

        try
        {
            _backupService.Restore(dataPath, areasPath);
            LoadPlants();
            LoadAreas();
            CanvasRefreshRequested?.Invoke();
            StatusText = "Backup restored successfully.";
            System.Windows.MessageBox.Show("Backup restored successfully!", "Restore Complete",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"Restore failed: {ex.Message}";
            System.Windows.MessageBox.Show($"Restore failed:\n{ex.Message}", "Restore Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void BackupNow()
    {
        if (string.IsNullOrWhiteSpace(_backupService.Settings.BackupFolderPath))
        {
            var prompt = System.Windows.MessageBox.Show(
                "No backup folder is configured. Open Backup Settings?",
                "Backup", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (prompt == System.Windows.MessageBoxResult.Yes) ConfigureBackup();
            return;
        }
        try
        {
            var dest = _backupService.Backup(isAuto: false);
            StatusText = $"Backup saved: {dest}";
            System.Windows.MessageBox.Show($"Backup saved to:\n{dest}", "Backup Complete",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"Backup failed: {ex.Message}";
            System.Windows.MessageBox.Show($"Backup failed:\n{ex.Message}", "Backup Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ConfigureBackup()
    {
        var dialog = new BackupSettingsDialog(_backupService.Settings) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            _backupService.Settings.BackupFolderPath = dialog.SelectedPath;
            _backupService.SaveSettings();
            StatusText = string.IsNullOrWhiteSpace(dialog.SelectedPath)
                ? "Backup folder cleared."
                : $"Backup folder set: {dialog.SelectedPath}";
        }
    }

    private void ChangeDataFolder()
    {
        var dialog = new DataFolderDialog(_dataService.AppDataDir)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        var newDir = dialog.SelectedPath;
        if (string.Equals(newDir, _dataService.AppDataDir, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            Directory.CreateDirectory(newDir);

            // Copy all data files and settings to the new location
            var filesToCopy = new[]
            {
                _dataService.DataFilePath,
                _dataService.AreasFilePath,
                _dataService.TasksFilePath,
                Path.Combine(_dataService.AppDataDir, "settings.json")
            };
            foreach (var src in filesToCopy)
            {
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(newDir, Path.GetFileName(src)!), overwrite: true);
            }

            // Update bootstrap (null = revert to default AppData dir)
            var defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GardenDiary");
            DataService.SaveCustomDir(
                string.Equals(newDir, defaultDir, StringComparison.OrdinalIgnoreCase) ? null : newDir);

            // Reinitialize services to point at new location
            _dataService   = new DataService(newDir);
            _backupService = new BackupService(_dataService.AppDataDir, _dataService.DataFilePath, _dataService.AreasFilePath);

            OnPropertyChanged(nameof(DataFolderPath));
            StatusText = $"Data folder changed to: {newDir}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to change data folder:\n{ex.Message}",
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── Tasks ──────────────────────────────────────────────────────────────────

    private void LoadTasksData()
    {
        Tasks.Clear();
        foreach (var t in _dataService.LoadTasks())
            Tasks.Add(t);
        RebuildTaskCards();
    }

    private void SaveTasksData() => _dataService.SaveTasks(Tasks);

    private void RebuildTaskCards()
    {
        TaskCards.Clear();
        CompletedTaskCards.Clear();
        var plantsList = (IReadOnlyList<Plant>)Plants;
        var areasList  = (IReadOnlyList<GardenArea>)Areas;
        foreach (var task in Tasks)
        {
            var card = new TaskCardViewModel(task, plantsList, areasList);
            if (task.Status == GardenTaskStatus.Completed)
                CompletedTaskCards.Add(card);
            else
                TaskCards.Add(card);
        }
        OnPropertyChanged(nameof(HasNoActiveTasks));
        OnPropertyChanged(nameof(HasCompletedTasks));
        OnPropertyChanged(nameof(CompletedTaskCount));
    }

    private void AddTask()
    {
        var dialog = new TaskEditDialog(new GardenTask { ReminderDate = DateTime.Today }, Plants, Areas)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        Tasks.Add(dialog.Task);
        SaveTasksData();
        RebuildTaskCards();
    }

    private void EditTask(GardenTask task)
    {
        var dialog = new TaskEditDialog(task, Plants, Areas) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        var idx = Tasks.IndexOf(task);
        if (idx < 0) return;
        Tasks[idx] = dialog.Task;
        SaveTasksData();
        RebuildTaskCards();
    }

    private void DeleteTask(GardenTask task)
    {
        var result = System.Windows.MessageBox.Show(
            $"Delete task '{task.Title}'?",
            "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        Tasks.Remove(task);
        SaveTasksData();
        RebuildTaskCards();
    }

    private void SkipTask(GardenTask task)
    {
        var result = System.Windows.MessageBox.Show(
            $"Skip '{task.Title}'? It will be marked as completed without logging any activities.",
            "Confirm Skip", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        task.Status        = GardenTaskStatus.Completed;
        task.CompletedDate = DateTime.Today;
        if (task.IsRepeating)
        {
            Tasks.Add(new GardenTask
            {
                Title        = task.Title,
                ReminderDate = DateTime.Today.AddDays(task.RepeatDays),
                IsLawnTask   = task.IsLawnTask,
                Activities   = new List<string>(task.Activities),
                PlantIds     = new List<Guid>(task.PlantIds),
                AreaIds      = new List<Guid>(task.AreaIds),
                IsRepeating  = task.IsRepeating,
                RepeatDays   = task.RepeatDays
            });
        }
        SaveTasksData();
        RebuildTaskCards();
    }

    private void CheckTaskCompletion()
    {
        var newTasks = new List<GardenTask>();
        foreach (var task in Tasks.Where(t => t.Status == GardenTaskStatus.Active))
        {
            var ids = task.IsLawnTask ? task.AreaIds : task.PlantIds;
            foreach (var id in ids.Where(id => !task.CompletedItemIds.Contains(id)))
            {
                if (ItemSatisfiesTask(id, task))
                    task.CompletedItemIds.Add(id);
            }
            if (ids.Count > 0 && task.CompletedItemIds.Intersect(ids).Count() >= ids.Count)
            {
                task.Status        = GardenTaskStatus.Completed;
                task.CompletedDate = DateTime.Today;
                if (task.IsRepeating)
                {
                    newTasks.Add(new GardenTask
                    {
                        Title        = task.Title,
                        ReminderDate = DateTime.Today.AddDays(task.RepeatDays),
                        IsLawnTask   = task.IsLawnTask,
                        Activities   = new List<string>(task.Activities),
                        PlantIds     = new List<Guid>(task.PlantIds),
                        AreaIds      = new List<Guid>(task.AreaIds),
                        IsRepeating  = true,
                        RepeatDays   = task.RepeatDays
                    });
                }
            }
        }
        foreach (var t in newTasks) Tasks.Add(t);
    }

    private bool ItemSatisfiesTask(Guid id, GardenTask task)
    {
        if (task.IsLawnTask)
        {
            var area = Areas.FirstOrDefault(a => a.Id == id);
            if (area == null) return false;
            return area.DiaryEntries.Any(e =>
                e.Date.Date >= task.ReminderDate.Date &&
                task.Activities.All(act => GetAreaActivityFlag(e, act)));
        }
        else
        {
            var plant = Plants.FirstOrDefault(p => p.Id == id);
            if (plant == null) return false;
            return plant.DiaryEntries.Any(e =>
                e.Date.Date >= task.ReminderDate.Date &&
                task.Activities.All(act => GetPlantActivityFlag(e, act)));
        }
    }

    private static bool GetPlantActivityFlag(DiaryEntry e, string act) => act switch
    {
        "Planting"               => e.Planting,
        "Watering"               => e.Watering,
        "Fertilizing & Mulching" => e.Fertilizing || e.Mulching,
        "Pruning"                => e.Pruning,
        _                        => false
    };

    private static bool GetAreaActivityFlag(AreaDiaryEntry e, string act) => act switch
    {
        "Mowing"      => e.Mowing,
        "Watering"    => e.Watering,
        "Overseeding" => e.Overseeding,
        "Feeding"     => e.Feeding,
        "Aerating"    => e.Aerating,
        _             => false
    };

    // ── Reminders ─────────────────────────────────────────────────────────────

    private static string FormatPlantDisplayName(string commonName, string? latin, string? variety)
    {
        var suffix = FormatLatinWithVariety(latin, variety);
        return string.IsNullOrEmpty(suffix) ? commonName : $"{commonName} ({suffix})";
    }

    private static string FormatLatinWithVariety(string? latin, string? variety)
    {
        var hasLatin   = !string.IsNullOrWhiteSpace(latin);
        var hasVariety = !string.IsNullOrWhiteSpace(variety);
        if (hasLatin && hasVariety) return $"{latin} '{variety}'";
        if (hasLatin)               return latin!;
        if (hasVariety)             return $"'{variety}'";
        return "";
    }

    private void RebuildReminders()
    {
        var q = _reminderSearchText.Trim();

        // Build plant view models grouped by area
        var plantVms = new List<(string areaKey, ReminderPlantViewModel vm)>();
        foreach (var plant in Plants.OrderBy(p => p.CommonName))
        {
            if (plant.DiaryEntries.Count == 0) continue;
            if (!string.IsNullOrEmpty(q))
            {
                bool matches = plant.CommonName.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                               (plant.LatinName ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                               (plant.Variety   ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase);
                if (!matches) continue;
            }
            var items = new List<ReminderItemViewModel>();
            foreach (var (name, selector, bg, fg) in ActivityDefs)
            {
                var matching = plant.DiaryEntries.Where(e => selector(e)).ToList();
                if (matching.Count == 0) continue;
                var lastDate = matching.Max(e => e.Date);
                items.Add(new ReminderItemViewModel(name, bg, fg, lastDate));
            }
            if (items.Count == 0) continue;
            var latinDisplay = FormatLatinWithVariety(plant.LatinName, plant.Variety);
            var areaName = Areas.FirstOrDefault(a => a.PlantPlacements.Any(p => p.PlantId == plant.Id))?.Name;
            plantVms.Add((areaName ?? "", new ReminderPlantViewModel(plant.Id, plant.CommonName, latinDisplay, items)));
        }

        // Preserve expanded state by area name, then rebuild groups
        var expandedState = PlantAreaGroups.ToDictionary(g => g.AreaName, g => g.IsExpanded);
        PlantAreaGroups.Clear();
        foreach (var group in plantVms
            .GroupBy(x => x.areaKey)
            .OrderBy(g => g.Key == "" ? 1 : 0)   // named areas first, unplaced last
            .ThenBy(g => g.Key))
        {
            var areaLabel = group.Key == "" ? "Unplaced" : group.Key;
            var grpVm = new PlantAreaGroupViewModel(areaLabel, group.Select(x => x.vm).ToList());
            if (expandedState.TryGetValue(areaLabel, out var wasExpanded))
                grpVm.IsExpanded = wasExpanded;
            PlantAreaGroups.Add(grpVm);
        }

        AreaReminders.Clear();
        foreach (var area in Areas.OrderBy(a => a.Name))
        {
            if (area.DiaryEntries.Count == 0) continue;
            if (!string.IsNullOrEmpty(q) && !area.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase))
                continue;
            var items = new List<ReminderItemViewModel>();
            foreach (var (name, selector, bg, fg) in AreaActivityDefs)
            {
                var matching = area.DiaryEntries.Where(e => selector(e)).ToList();
                if (matching.Count == 0) continue;
                var lastDate = matching.Max(e => e.Date);
                items.Add(new ReminderItemViewModel(name, bg, fg, lastDate));
            }
            if (items.Count == 0) continue;
            AreaReminders.Add(new ReminderAreaViewModel(area.Name, items));
        }

        OnPropertyChanged(nameof(PlantAreaGroups));
        OnPropertyChanged(nameof(AreaReminders));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
