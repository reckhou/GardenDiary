namespace GardenDiary.Models;

public enum GardenTaskStatus { Active, Completed }

public class GardenTask
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string Title         { get; set; } = "";
    public DateTime ReminderDate { get; set; }
    public bool   IsLawnTask    { get; set; }
    public List<string> Activities     { get; set; } = new();
    public List<Guid>   PlantIds       { get; set; } = new();
    public List<Guid>   AreaIds        { get; set; } = new();
    public bool   IsRepeating   { get; set; }
    public int    RepeatDays    { get; set; }
    public GardenTaskStatus Status { get; set; } = GardenTaskStatus.Active;
    public DateTime? CompletedDate    { get; set; }
    public List<Guid> CompletedItemIds { get; set; } = new();
}
