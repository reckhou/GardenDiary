namespace GardenDiary.Models;

public class GeneralDiaryEntry
{
    public Guid       Id       { get; set; } = Guid.NewGuid();
    public DateTime   Date     { get; set; } = DateTime.Today;
    public string     Notes    { get; set; } = "";
    public List<Guid> PlantIds { get; set; } = new();
}
