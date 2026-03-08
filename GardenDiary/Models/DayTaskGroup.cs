namespace GardenDiary.Models;

public class DayTaskGroup
{
    public string ActivityName { get; set; } = "";
    public string BadgeBackground { get; set; } = "#E8F5E9";
    public string BadgeForeground { get; set; } = "#2E7D32";
    public List<PlantSummary> Plants { get; set; } = new();
}
