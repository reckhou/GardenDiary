namespace GardenDiary.Models;

public class AppSettings
{
    public string BackupFolderPath { get; set; } = "";
    public DateOnly? LastAutoBackupDate { get; set; }
}
