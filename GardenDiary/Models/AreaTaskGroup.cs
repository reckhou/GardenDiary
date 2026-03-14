namespace GardenDiary.Models;

public class AreaTaskGroup
{
    public string ActivityName    { get; set; } = "";
    public string BadgeBackground { get; set; } = "#DCEDC8";
    public string BadgeForeground { get; set; } = "#33691E";
    public List<AreaSummary> Areas { get; set; } = new();
}
