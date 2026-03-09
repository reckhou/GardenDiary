using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using GardenDiary.Models;
using GardenDiary.Services;
using GardenDiary.Views;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DataService _dataService = new();
    private readonly BackupService _backupService;
    private readonly WeatherService _weatherService = new();

    private Plant? _selectedPlant;
    private DiaryEntry? _selectedEntry;
    private string _statusText = "Ready";
    private DateTime? _selectedCalendarDate;
    private DayWeather? _dayWeather;
    private bool _isLoadingWeather;

    // Garden planner
    private GardenArea? _selectedArea;
    private Plant? _plantToPlace;
    private PlantOption? _selectedPlantOption;
    private PlantPlacement? _selectedPlacement;

    public ObservableCollection<Plant> Plants { get; } = new();
    public ObservableCollection<DiaryEntry> Entries { get; } = new();
    public ObservableCollection<DayTaskGroup> DayTasks { get; } = new();
    public ObservableCollection<GardenArea> Areas { get; } = new();
    public ObservableCollection<PlantOption> PlantOptions { get; } = new();

    // ── Plants & Diary ────────────────────────────────────────────────────────

    public Plant? SelectedPlant
    {
        get => _selectedPlant;
        set
        {
            _selectedPlant = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPlantTitle));
            LoadEntries();
        }
    }

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
            LoadDayTasks();
            _ = LoadWeatherAsync();
        }
    }

    public string DayTasksTitle => _selectedCalendarDate.HasValue
        ? _selectedCalendarDate.Value.ToString("dddd, MMMM d, yyyy")
        : "Select a day on the calendar";

    public bool NoDayTasks => DayTasks.Count == 0;

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
        get => _plantToPlace?.DefaultRadius ?? 30;
        set
        {
            if (_plantToPlace == null || value <= 0) return;
            _plantToPlace.DefaultRadius = value;
            OnPropertyChanged();
            Save();
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
            CanvasRefreshRequested?.Invoke();
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

    // Raised when the canvas must be redrawn
    public event Action? CanvasRefreshRequested;

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand AddPlantCommand { get; }
    public RelayCommand EditPlantCommand { get; }
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

    public MainViewModel()
    {
        _backupService = new BackupService(_dataService.AppDataDir, _dataService.DataFilePath, _dataService.AreasFilePath);

        AddPlantCommand         = new RelayCommand(_ => AddPlant());
        EditPlantCommand        = new RelayCommand(_ => EditPlant(),   _ => SelectedPlant != null);
        DeletePlantCommand      = new RelayCommand(_ => DeletePlant(), _ => SelectedPlant != null);
        AddEntryCommand         = new RelayCommand(_ => AddEntry(),    _ => SelectedPlant != null);
        EditEntryCommand        = new RelayCommand(_ => EditEntry(),   _ => SelectedEntry != null);
        DeleteEntryCommand      = new RelayCommand(_ => DeleteEntry(), _ => SelectedEntry != null);
        BackupNowCommand        = new RelayCommand(_ => BackupNow());
        ConfigureBackupCommand  = new RelayCommand(_ => ConfigureBackup());
        CalendarAddEntryCommand  = new RelayCommand(_ => CalendarAddEntry(), _ => SelectedCalendarDate.HasValue);
        EditCalendarEntryCommand = new RelayCommand(obj => { if (obj is Guid id) EditCalendarEntry(id); });
        AddAreaCommand          = new RelayCommand(_ => AddArea());
        EditAreaCommand         = new RelayCommand(_ => EditArea(),    _ => SelectedArea != null);
        DeleteAreaCommand       = new RelayCommand(_ => DeleteArea(),  _ => SelectedArea != null);
        DeletePlacementCommand  = new RelayCommand(_ => DeletePlacement(), _ => SelectedPlacement != null);
        RestoreBackupCommand    = new RelayCommand(_ => RestoreBackup());
        SetHomeLocationCommand  = new RelayCommand(_ => SetHomeLocation());

        LoadPlants();
        LoadAreas();
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

        // Update availability for all options
        foreach (var opt in PlantOptions)
        {
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
    }

    private void LoadEntries()
    {
        Entries.Clear();
        if (_selectedPlant == null) return;
        foreach (var e in _selectedPlant.DiaryEntries.OrderByDescending(e => e.Date))
            Entries.Add(e);
    }

    private static readonly (string Name, Func<DiaryEntry, bool> Selector, string Bg, string Fg)[] ActivityDefs =
    {
        ("Planting",    e => e.Planting,    "#C8E6C9", "#1B5E20"),
        ("Watering",    e => e.Watering,    "#BBDEFB", "#0D47A1"),
        ("Fertilizing", e => e.Fertilizing, "#FFE0B2", "#BF360C"),
        ("Weeding",     e => e.Weeding,     "#D7CCC8", "#3E2723"),
        ("Mulching",    e => e.Mulching,    "#FFF9C4", "#F57F17"),
        ("Pruning",     e => e.Pruning,     "#E1BEE7", "#4A148C"),
    };

    private void LoadDayTasks()
    {
        DayTasks.Clear();
        if (!_selectedCalendarDate.HasValue) return;

        var date = _selectedCalendarDate.Value.Date;

        foreach (var (name, selector, bg, fg) in ActivityDefs)
        {
            var plants = Plants
                .Where(p => p.DiaryEntries.Any(e => e.Date.Date == date && selector(e)))
                .Select(p =>
                {
                    var entry = p.DiaryEntries.First(e => e.Date.Date == date && selector(e));
                    return new PlantSummary
                    {
                        PlantId   = p.Id,
                        Name      = string.IsNullOrWhiteSpace(p.Variety) ? p.CommonName : $"{p.CommonName} ({p.Variety})",
                        LatinName = p.LatinName,
                        Notes     = entry.Notes ?? ""
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
    }

    private void Save()
    {
        _dataService.SavePlants(Plants);
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
            SelectedPlant = dialog.Plant;
        }
    }

    private void EditPlant()
    {
        if (SelectedPlant == null) return;
        var copy = new Plant
        {
            Id            = SelectedPlant.Id,
            CommonName    = SelectedPlant.CommonName,
            LatinName     = SelectedPlant.LatinName,
            Variety       = SelectedPlant.Variety,
            DefaultRadius = SelectedPlant.DefaultRadius,
            DiaryEntries  = SelectedPlant.DiaryEntries
        };
        var dialog = new PlantEditDialog(copy) { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            var idx = Plants.IndexOf(SelectedPlant);
            Plants[idx] = dialog.Plant;
            Save();
            SelectedPlant = Plants[idx];
        }
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
        Save();
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
            Save();
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
            Save();
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
        Save();
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    private void CalendarAddEntry()
    {
        if (!_selectedCalendarDate.HasValue) return;
        var dialog = new CalendarEntryDialog(_selectedCalendarDate.Value, Plants.ToList())
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveCalendarEntry(dialog.SelectedPlant!, dialog.Entry);
    }

    private void EditCalendarEntry(Guid plantId)
    {
        if (!_selectedCalendarDate.HasValue) return;
        var plant = Plants.FirstOrDefault(p => p.Id == plantId);
        if (plant == null) return;
        var dialog = new CalendarEntryDialog(_selectedCalendarDate.Value, Plants.ToList(), plant)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveCalendarEntry(dialog.SelectedPlant!, dialog.Entry);
    }

    // Called from garden planner double-click
    public void OpenActivityDialogForPlacement(Guid plantId)
    {
        var plant = Plants.FirstOrDefault(p => p.Id == plantId);
        if (plant == null) return;
        var dialog = new CalendarEntryDialog(DateTime.Today, Plants.ToList(), plant)
            { Owner = App.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        SaveCalendarEntry(dialog.SelectedPlant!, dialog.Entry);
    }

    private void SaveCalendarEntry(Plant plant, DiaryEntry newEntry)
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
            plant.DiaryEntries.Add(newEntry);
        }
        if (SelectedPlant?.Id == plant.Id) LoadEntries();
        Save();
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
        var copy = new GardenArea
        {
            Id              = SelectedArea.Id,
            Name            = SelectedArea.Name,
            Width           = SelectedArea.Width,
            Height          = SelectedArea.Height,
            PlantPlacements = SelectedArea.PlantPlacements
        };
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
            Radius  = DefaultPlacementRadius
        };
        SelectedArea.PlantPlacements.Add(placement);
        SaveAreas();
        RefreshPlantOptions();
        return placement;
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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
