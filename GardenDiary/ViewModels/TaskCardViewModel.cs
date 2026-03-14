using GardenDiary.Models;

namespace GardenDiary.ViewModels;

public class TaskCardViewModel
{
    public GardenTask Task { get; }
    public string Title                  { get; }
    public string ReminderDateLabel      { get; }
    public bool   IsOverdue              { get; }
    public string OverdueBadge           { get; }
    public string RequiredActivitiesLabel { get; }
    public string TaskTypeLabel          { get; }
    public List<string> DoneItems        { get; }
    public List<string> PendingItems     { get; }
    public bool HasDoneItems             { get; }
    public bool HasPendingItems          { get; }
    public bool IsCompleted              { get; }
    public string CompletedDateLabel     { get; }

    public TaskCardViewModel(
        GardenTask task,
        IReadOnlyList<Plant> plants,
        IReadOnlyList<GardenArea> areas)
    {
        Task  = task;
        Title = task.Title;

        ReminderDateLabel = task.ReminderDate.ToString("MMMM d, yyyy");
        IsOverdue         = task.Status == GardenTaskStatus.Active
                            && task.ReminderDate.Date < DateTime.Today;
        OverdueBadge      = IsOverdue
            ? $"Overdue by {(DateTime.Today - task.ReminderDate.Date).Days} day(s)"
            : "";

        RequiredActivitiesLabel = string.Join(", ", task.Activities);
        TaskTypeLabel           = task.IsLawnTask ? "Lawn" : "Plants";
        IsCompleted             = task.Status == GardenTaskStatus.Completed;
        CompletedDateLabel      = IsCompleted && task.CompletedDate.HasValue
            ? $"Completed {task.CompletedDate.Value:MMMM d, yyyy}"
            : "";

        // Build done / pending name lists
        if (task.IsLawnTask)
        {
            var areaById = areas.ToDictionary(a => a.Id, a => a.Name);
            DoneItems    = task.CompletedItemIds
                .Where(id => task.AreaIds.Contains(id))
                .Select(id => areaById.TryGetValue(id, out var n) ? n : id.ToString())
                .ToList();
            PendingItems = task.AreaIds
                .Where(id => !task.CompletedItemIds.Contains(id))
                .Select(id => areaById.TryGetValue(id, out var n) ? n : id.ToString())
                .ToList();
        }
        else
        {
            var plantById = plants.ToDictionary(p => p.Id, p =>
            {
                var suffix = FormatLatinWithVariety(p.LatinName, p.Variety);
                return string.IsNullOrEmpty(suffix) ? p.CommonName : $"{p.CommonName} ({suffix})";
            });
            DoneItems    = task.CompletedItemIds
                .Where(id => task.PlantIds.Contains(id))
                .Select(id => plantById.TryGetValue(id, out var n) ? n : id.ToString())
                .ToList();
            PendingItems = task.PlantIds
                .Where(id => !task.CompletedItemIds.Contains(id))
                .Select(id => plantById.TryGetValue(id, out var n) ? n : id.ToString())
                .ToList();
        }

        HasDoneItems    = DoneItems.Count > 0;
        HasPendingItems = PendingItems.Count > 0;
    }

    private static string FormatLatinWithVariety(string? latin, string? variety)
    {
        var hasLatin   = !string.IsNullOrWhiteSpace(latin);
        var hasVariety = !string.IsNullOrWhiteSpace(variety);
        if (hasLatin && hasVariety) return $"{latin} '{variety}'";
        if (hasLatin)               return latin!;
        if (hasVariety)             return $"'{variety}'";
        return "";
    }
}
