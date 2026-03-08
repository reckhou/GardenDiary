using System.IO;
using GardenDiary.Models;
using GardenDiary.Services;
using Xunit;

namespace GardenDiary.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dataFile;
    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _dataFile = Path.Combine(_tempDir, "data.json");
        _sut      = new BackupService(_tempDir, _dataFile);
    }

    [Fact]
    public void ShouldAutoBackup_WhenNoPathConfigured_ReturnsFalse()
    {
        _sut.Settings.BackupFolderPath = "";

        Assert.False(_sut.ShouldAutoBackup());
    }

    [Fact]
    public void ShouldAutoBackup_WhenBackedUpToday_ReturnsFalse()
    {
        _sut.Settings.BackupFolderPath  = _tempDir;
        _sut.Settings.LastAutoBackupDate = DateOnly.FromDateTime(DateTime.Today);

        Assert.False(_sut.ShouldAutoBackup());
    }

    [Fact]
    public void ShouldAutoBackup_WhenNotBackedUpToday_ReturnsTrue()
    {
        _sut.Settings.BackupFolderPath   = _tempDir;
        _sut.Settings.LastAutoBackupDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        Assert.True(_sut.ShouldAutoBackup());
    }

    [Fact]
    public void ShouldAutoBackup_WhenNeverBackedUp_ReturnsTrue()
    {
        _sut.Settings.BackupFolderPath   = _tempDir;
        _sut.Settings.LastAutoBackupDate = null;

        Assert.True(_sut.ShouldAutoBackup());
    }

    [Fact]
    public void Backup_WhenDataFileMissing_Throws()
    {
        _sut.Settings.BackupFolderPath = _tempDir;

        Assert.Throws<InvalidOperationException>(() => _sut.Backup(isAuto: false));
    }

    [Fact]
    public void Backup_WhenNoPathConfigured_Throws()
    {
        File.WriteAllText(_dataFile, "[]");
        _sut.Settings.BackupFolderPath = "";

        Assert.Throws<InvalidOperationException>(() => _sut.Backup(isAuto: false));
    }

    [Fact]
    public void Backup_Manual_CreatesTimestampedFile()
    {
        File.WriteAllText(_dataFile, "[{\"CommonName\":\"Rose\"}]");
        var backupDir = Path.Combine(_tempDir, "backups");
        _sut.Settings.BackupFolderPath = backupDir;

        var dest = _sut.Backup(isAuto: false);

        Assert.True(File.Exists(dest));
        Assert.Contains("GardenDiary_manual_", Path.GetFileName(dest));
        Assert.Equal("[{\"CommonName\":\"Rose\"}]", File.ReadAllText(dest));
    }

    [Fact]
    public void Backup_Auto_CreatesDateNamedFile()
    {
        File.WriteAllText(_dataFile, "[]");
        var backupDir = Path.Combine(_tempDir, "backups");
        _sut.Settings.BackupFolderPath = backupDir;

        var dest = _sut.Backup(isAuto: true);

        Assert.True(File.Exists(dest));
        Assert.Contains("GardenDiary_auto_", Path.GetFileName(dest));
    }

    [Fact]
    public void Backup_Auto_UpdatesLastAutoBackupDate()
    {
        File.WriteAllText(_dataFile, "[]");
        _sut.Settings.BackupFolderPath   = _tempDir;
        _sut.Settings.LastAutoBackupDate = null;

        _sut.Backup(isAuto: true);

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), _sut.Settings.LastAutoBackupDate);
    }

    [Fact]
    public void Backup_Auto_ShouldAutoBackup_ReturnsFalseAfterwards()
    {
        File.WriteAllText(_dataFile, "[]");
        _sut.Settings.BackupFolderPath   = _tempDir;
        _sut.Settings.LastAutoBackupDate = null;

        _sut.Backup(isAuto: true);

        Assert.False(_sut.ShouldAutoBackup());
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
