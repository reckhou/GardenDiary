namespace GardenDiary.Models;

public class DiaryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public bool Planting { get; set; }
    public bool Watering { get; set; }
    public bool Fertilizing { get; set; }
    public bool Weeding { get; set; }
    public bool Mulching { get; set; }
    public bool Pruning { get; set; }
    public string Notes { get; set; } = "";
}
