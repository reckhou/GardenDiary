namespace GardenDiary.ViewModels;

public class ReminderAreaViewModel
{
    public string AreaName { get; }
    public List<ReminderItemViewModel> Items { get; }

    public ReminderAreaViewModel(string areaName, List<ReminderItemViewModel> items)
    {
        AreaName = areaName;
        Items    = items;
    }
}
