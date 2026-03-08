using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GardenDiary.Models;
using GardenDiary.Services;
using GardenDiary.Views;

namespace GardenDiary.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DataService _dataService = new();
    private readonly BackupService _backupService;

    private Plant? _selectedPlant;
    private DiaryEntry? _selectedEntry;
    private string _statusText = "Ready";
    private DateTime? _selectedCalendarDate;

    public ObservableCollection<Plant> Plants { get; } = new();
    public ObservableCollection<DiaryEntry> Entries { get; } = new();
    public ObservableCollection<DayTaskGroup> DayTasks { get; } = new();

    // ── Plants & Diary properties ─────────────────────────────────────────────

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

    // ── Calendar properties ───────────────────────────────────────────────────

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
        }
    }

    public string DayTasksTitle => _selectedCalendarDate.HasValue
        ? _selectedCalendarDate.Value.ToString("dddd, MMMM d, yyyy")
        : "Select a day on the calendar";

    public bool NoDayTasks => DayTasks.Count == 0;

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

    public MainViewModel()
    {
        _backupService = new BackupService(_dataService.AppDataDir, _dataService.DataFilePath);

        AddPlantCommand        = new RelayCommand(_ => AddPlant());
        EditPlantCommand       = new RelayCommand(_ => EditPlant(),  _ => SelectedPlant != null);
        DeletePlantCommand     = new RelayCommand(_ => DeletePlant(), _ => SelectedPlant != null);
        AddEntryCommand        = new RelayCommand(_ => AddEntry(),   _ => SelectedPlant != null);
        EditEntryCommand       = new RelayCommand(_ => EditEntry(),  _ => SelectedEntry != null);
        DeleteEntryCommand     = new RelayCommand(_ => DeleteEntry(), _ => SelectedEntry != null);
        BackupNowCommand       = new RelayCommand(_ => BackupNow());
        ConfigureBackupCommand = new RelayCommand(_ => ConfigureBackup());
        CalendarAddEntryCommand = new RelayCommand(_ => CalendarAddEntry(), _ => SelectedCalendarDate.HasValue);

        LoadPlants();
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
                .Select(p => new PlantSummary
                {
                    Name = string.IsNullOrWhiteSpace(p.Variety) ? p.CommonName : $"{p.CommonName} ({p.Variety})",
                    LatinName = p.LatinName
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
        LoadDayTasks(); // keep calendar in sync after any data change
    }

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
            Id = SelectedPlant.Id,
            CommonName = SelectedPlant.CommonName,
            LatinName = SelectedPlant.LatinName,
            Variety = SelectedPlant.Variety,
            DiaryEntries = SelectedPlant.DiaryEntries
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
            $"Delete '{SelectedPlant.CommonName}' and all its diary entries?",
            "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        Plants.Remove(SelectedPlant);
        SelectedPlant = null;
        Save();
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

        var dialog = new CalendarEntryDialog(
            _selectedCalendarDate.Value,
            Plants.ToList())
        { Owner = App.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        var plant = dialog.SelectedPlant!;
        var newEntry = dialog.Entry;

        // Find existing entry for same plant + date and merge, or add new
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

        // Refresh per-plant diary panel if this plant is currently selected
        if (SelectedPlant?.Id == plant.Id) LoadEntries();

        Save();
    }

    // ── Backup ────────────────────────────────────────────────────────────────

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
        var dialog = new BackupSettingsDialog(_backupService.Settings)
            { Owner = App.Current.MainWindow };
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
