using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GardenDiary.ViewModels;

public class PlantAreaGroupViewModel : INotifyPropertyChanged
{
    public string                      AreaName   { get; }
    public List<ReminderPlantViewModel> Plants     { get; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public PlantAreaGroupViewModel(string areaName, List<ReminderPlantViewModel> plants)
    {
        AreaName = areaName;
        Plants   = plants;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
