namespace GardenDiary.Models;

public class AreaDiaryEntry
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public DateTime Date        { get; set; }
    public bool     Mowing      { get; set; }
    public bool     Watering    { get; set; }
    public bool     Overseeding { get; set; }
    public bool     Feeding     { get; set; }
    public bool     Aerating    { get; set; }
    public string?  Notes       { get; set; }
}
