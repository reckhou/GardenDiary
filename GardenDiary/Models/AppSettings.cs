namespace GardenDiary.Models;

public class AppSettings
{
    public string BackupFolderPath { get; set; } = "";
    public DateOnly? LastAutoBackupDate { get; set; }
    public Guid? LastSelectedAreaId { get; set; }
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
}
