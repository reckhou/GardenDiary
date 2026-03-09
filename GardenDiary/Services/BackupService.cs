using System.IO;
using System.Text.Json;
using GardenDiary.Models;

namespace GardenDiary.Services;

public class BackupService
{
    private readonly string _settingsPath;
    private readonly string _dataFilePath;
    private readonly string? _areasFilePath;
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public AppSettings Settings { get; private set; }

    public BackupService(string appDataDir, string dataFilePath, string? areasFilePath = null)
    {
        _settingsPath  = Path.Combine(appDataDir, "settings.json");
        _dataFilePath  = dataFilePath;
        _areasFilePath = areasFilePath;
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

        if (_areasFilePath != null && File.Exists(_areasFilePath))
        {
            string areasName = isAuto
                ? $"GardenDiary_areas_auto_{DateTime.Today:yyyy-MM-dd}.json"
                : $"GardenDiary_areas_manual_{DateTime.Now:yyyy-MM-dd_HHmmss}.json";
            File.Copy(_areasFilePath, Path.Combine(Settings.BackupFolderPath, areasName), overwrite: true);
        }

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

    // ── Restore helpers ───────────────────────────────────────────────────────

    private const string AreasInfix = "GardenDiary_areas_";
    private const string DataPrefix = "GardenDiary_";

    /// <summary>Returns true if the file is an areas backup (contains "_areas_").</summary>
    public static bool IsAreasBackupFile(string path)
        => Path.GetFileName(path).StartsWith(AreasInfix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Given a data backup path, derives the areas backup path.</summary>
    public static string GetAreasBackupPath(string dataBackupPath)
    {
        var dir  = Path.GetDirectoryName(dataBackupPath) ?? ".";
        var name = Path.GetFileName(dataBackupPath);
        var areasName = name.StartsWith(DataPrefix, StringComparison.OrdinalIgnoreCase)
            ? AreasInfix + name[DataPrefix.Length..]
            : AreasInfix + name;
        return Path.Combine(dir, areasName);
    }

    /// <summary>Given an areas backup path, derives the data backup path.</summary>
    public static string GetDataBackupPath(string areasBackupPath)
    {
        var dir  = Path.GetDirectoryName(areasBackupPath) ?? ".";
        var name = Path.GetFileName(areasBackupPath);
        var dataName = name.StartsWith(AreasInfix, StringComparison.OrdinalIgnoreCase)
            ? DataPrefix + name[AreasInfix.Length..]
            : name;
        return Path.Combine(dir, dataName);
    }

    /// <summary>
    /// Creates a safety backup before a restore. Uses the configured backup folder,
    /// falling back to the AppData folder.
    /// </summary>
    public void SafetyBackup()
    {
        var folder = !string.IsNullOrWhiteSpace(Settings.BackupFolderPath)
            ? Settings.BackupFolderPath
            : Path.GetDirectoryName(_dataFilePath)!;
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");

        if (File.Exists(_dataFilePath))
            File.Copy(_dataFilePath,
                Path.Combine(folder, $"GardenDiary_pre_restore_{stamp}.json"), overwrite: true);
        if (_areasFilePath != null && File.Exists(_areasFilePath))
            File.Copy(_areasFilePath,
                Path.Combine(folder, $"GardenDiary_areas_pre_restore_{stamp}.json"), overwrite: true);
    }

    /// <summary>
    /// Restores both data and areas files from backup. Both files must exist.
    /// </summary>
    public void Restore(string dataBackupPath, string areasBackupPath)
    {
        if (!File.Exists(dataBackupPath))
            throw new FileNotFoundException("Data backup file not found.", dataBackupPath);
        if (!File.Exists(areasBackupPath))
            throw new FileNotFoundException("Areas backup file not found.", areasBackupPath);

        File.Copy(dataBackupPath, _dataFilePath, overwrite: true);
        if (_areasFilePath != null)
            File.Copy(areasBackupPath, _areasFilePath, overwrite: true);
    }
}
