namespace GardenDiary.ViewModels;

public class ReminderItemViewModel
{
    public string ActivityName    { get; }
    public string BadgeBackground { get; }
    public string BadgeForeground { get; }
    public string LastDateLabel   { get; }
    public string DaysAgoLabel    { get; }

    public ReminderItemViewModel(string activityName, string bg, string fg, DateTime lastDate)
    {
        ActivityName    = activityName;
        BadgeBackground = bg;
        BadgeForeground = fg;
        LastDateLabel   = lastDate.ToString("MMMM d, yyyy");

        var days = (DateTime.Today - lastDate.Date).Days;
        DaysAgoLabel = days == 0 ? "(today)"
                     : days == 1 ? "(yesterday)"
                     : $"({days} days ago)";
    }
}
