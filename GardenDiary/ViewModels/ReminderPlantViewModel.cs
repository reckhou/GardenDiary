namespace GardenDiary.ViewModels;

public class ReminderPlantViewModel
{
    public Guid   PlantId          { get; }
    public string PlantDisplayName { get; }
    public string LatinName        { get; }
    public bool   HasLatinName     { get; }
    public List<ReminderItemViewModel> Items { get; }

    public ReminderPlantViewModel(Guid plantId, string displayName, string latinName, List<ReminderItemViewModel> items)
    {
        PlantId          = plantId;
        PlantDisplayName = displayName;
        LatinName        = latinName ?? "";
        HasLatinName     = !string.IsNullOrWhiteSpace(latinName);
        Items            = items;
    }
}
