using System.ComponentModel;
using System.Runtime.CompilerServices;
using GardenDiary.Models;

namespace GardenDiary.ViewModels;

public class PlantOption : INotifyPropertyChanged
{
    private bool _isAvailable = true;
    private string _placedAreaName = "";
    private Plant _plant;

    public Plant Plant
    {
        get => _plant;
        set { _plant = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set { _isAvailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public string PlacedAreaName
    {
        get => _placedAreaName;
        set { _placedAreaName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public string DisplayText => IsAvailable
        ? Plant.CommonName
        : $"{Plant.CommonName}  (in {PlacedAreaName})";

    public PlantOption(Plant plant) => _plant = plant;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
