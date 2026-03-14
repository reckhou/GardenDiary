namespace GardenDiary.Models;

public class AreaSummary
{
    public Guid   AreaId     { get; set; }
    public string Name       { get; set; } = "";
    public string Activities { get; set; } = "";
    public string Notes      { get; set; } = "";
}
