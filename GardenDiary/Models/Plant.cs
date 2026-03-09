namespace GardenDiary.Models;

public class Plant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommonName { get; set; } = "";
    public string LatinName { get; set; } = "";
    public string Variety { get; set; } = "";
    public List<DiaryEntry> DiaryEntries { get; set; } = new();
    public double DefaultRadius { get; set; } = 30;
}
