using System.Windows;
using System.Windows.Controls;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class CalendarEntryDialog : Window
{
    private readonly DateTime _date;
    private readonly List<Plant> _plants;

    public Plant? SelectedPlant { get; private set; }
    public DiaryEntry Entry { get; private set; } = new();

    public CalendarEntryDialog(DateTime date, List<Plant> plants, Plant? preselect = null)
    {
        InitializeComponent();
        _date = date;
        _plants = plants;

        TxtDate.Text = date.ToString("dddd, MMMM d, yyyy");

        CmbPlant.ItemsSource = plants;
        CmbPlant.SelectedItem = preselect ?? plants.FirstOrDefault();
    }

    private void CmbPlant_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPlant.SelectedItem is not Plant plant) return;

        // Pre-fill checkboxes from existing entry for this plant+date (if any)
        var existing = plant.DiaryEntries.FirstOrDefault(en => en.Date.Date == _date.Date);
        ChkPlanting.IsChecked    = existing?.Planting    ?? false;
        ChkWatering.IsChecked    = existing?.Watering    ?? false;
        ChkFertilizing.IsChecked = existing?.Fertilizing ?? false;
        ChkWeeding.IsChecked     = existing?.Weeding     ?? false;
        ChkMulching.IsChecked    = existing?.Mulching    ?? false;
        ChkPruning.IsChecked     = existing?.Pruning     ?? false;
        TxtNotes.Text            = existing?.Notes       ?? "";
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (CmbPlant.SelectedItem is not Plant plant)
        {
            MessageBox.Show("Please select a plant.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SelectedPlant = plant;
        Entry = new DiaryEntry
        {
            Date        = _date.Date,
            Planting    = ChkPlanting.IsChecked    == true,
            Watering    = ChkWatering.IsChecked    == true,
            Fertilizing = ChkFertilizing.IsChecked == true,
            Weeding     = ChkWeeding.IsChecked     == true,
            Mulching    = ChkMulching.IsChecked    == true,
            Pruning     = ChkPruning.IsChecked     == true,
            Notes       = TxtNotes.Text.Trim()
        };
        DialogResult = true;
    }
}
