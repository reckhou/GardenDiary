using System.IO;
using System.Text.Json;
using GardenDiary.Models;

namespace GardenDiary.Services;

public class BackupService
{
    private readonly string _settingsPath;
    private readonly string _dataFilePath;
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public AppSettings Settings { get; private set; }

    public BackupService(string appDataDir, string dataFilePath)
    {
        _settingsPath = Path.Combine(appDataDir, "settings.json");
        _dataFilePath = dataFilePath;
        Settings = LoadSettings();
    }

    // ── Settings persistence ─────────────────────────────────────────────────

    private AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, _options);
        File.WriteAllText(_settingsPath, json);
    }

    // ── Backup logic ─────────────────────────────────────────────────────────

    /// <summary>
    /// Copies the data file to the backup folder.
    /// Returns the path of the created backup file, or throws on failure.
    /// </summary>
    public string Backup(bool isAuto)
    {
        if (!File.Exists(_dataFilePath))
            throw new InvalidOperationException("No data file found to back up.");

        if (string.IsNullOrWhiteSpace(Settings.BackupFolderPath))
            throw new InvalidOperationException("Backup folder path is not configured.");

        Directory.CreateDirectory(Settings.BackupFolderPath);

        string fileName = isAuto
            ? $"GardenDiary_auto_{DateTime.Today:yyyy-MM-dd}.json"
            : $"GardenDiary_manual_{DateTime.Now:yyyy-MM-dd_HHmmss}.json";

        string dest = Path.Combine(Settings.BackupFolderPath, fileName);
        File.Copy(_dataFilePath, dest, overwrite: true);

        if (isAuto)
            Settings.LastAutoBackupDate = DateOnly.FromDateTime(DateTime.Today);

        SaveSettings();
        return dest;
    }

    /// <summary>
    /// Returns true if an auto-backup should run today (path configured and not yet backed up today).
    /// </summary>
    public bool ShouldAutoBackup()
        => !string.IsNullOrWhiteSpace(Settings.BackupFolderPath)
           && Settings.LastAutoBackupDate != DateOnly.FromDateTime(DateTime.Today);
}
