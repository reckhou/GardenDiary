namespace GardenDiary.Models;

public class Plant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommonName { get; set; } = "";
    public string LatinName { get; set; } = "";
    public string Variety { get; set; } = "";
    public string Emoji { get; set; } = "";
    public List<DiaryEntry> DiaryEntries { get; set; } = new();
    public double DefaultRadius { get; set; } = 30;

    public Plant Clone() => new()
    {
        Id            = Id,
        CommonName    = CommonName,
        LatinName     = LatinName,
        Variety       = Variety,
        Emoji         = Emoji,
        DefaultRadius = DefaultRadius,
        DiaryEntries  = DiaryEntries
    };
}
