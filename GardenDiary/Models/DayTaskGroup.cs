using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GardenDiary.Models;

public class DayTaskGroup : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string ActivityName { get; set; } = "";
    public string BadgeBackground { get; set; } = "#E8F5E9";
    public string BadgeForeground { get; set; } = "#2E7D32";
    public List<PlantSummary> Plants { get; set; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChevronText)); }
    }

    public string ChevronText => _isExpanded ? "▼" : "▶";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
