namespace GardenDiary.Models;

public class PlantSummary
{
    public Guid   PlantId   { get; set; }
    public string Name      { get; set; } = "";
    public string LatinName { get; set; } = "";
    public string Notes     { get; set; } = "";
}
