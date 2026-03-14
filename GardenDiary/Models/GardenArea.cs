namespace GardenDiary.Models;

public class GardenArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public double Width { get; set; } = 500;
    public double Height { get; set; } = 400;
    public List<PlantPlacement> PlantPlacements { get; set; } = new();
    public List<GardenShape> Shapes { get; set; } = new();
    public List<AreaDiaryEntry> DiaryEntries { get; set; } = new();
}
