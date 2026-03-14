namespace GardenDiary.ViewModels;

public class ReminderPlantViewModel
{
    public string PlantDisplayName { get; }
    public string LatinName        { get; }
    public bool   HasLatinName     { get; }
    public List<ReminderItemViewModel> Items { get; }

    public ReminderPlantViewModel(string displayName, string latinName, List<ReminderItemViewModel> items)
    {
        PlantDisplayName = displayName;
        LatinName        = latinName ?? "";
        HasLatinName     = !string.IsNullOrWhiteSpace(latinName);
        Items            = items;
    }
}
