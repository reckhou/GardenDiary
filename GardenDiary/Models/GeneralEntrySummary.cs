namespace GardenDiary.Models;

/// <summary>Display-only view model for a GeneralDiaryEntry in the calendar panel.</summary>
public class GeneralEntrySummary
{
    public Guid   Id            { get; set; }
    public string Notes         { get; set; } = "";
    public string PlantSummary  { get; set; } = "";
}
