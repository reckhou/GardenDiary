namespace GardenDiary.Models;

public class PlantPlacement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlantId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; } = 30;
}
