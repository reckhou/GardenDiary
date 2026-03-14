using System.Windows;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class DiaryEntryEditDialog : Window
{
    public DiaryEntry Entry { get; private set; }

    public DiaryEntryEditDialog(DiaryEntry entry)
    {
        InitializeComponent();
        Entry = entry;
        DpDate.SelectedDate = entry.Date;
        ChkWatering.IsChecked    = entry.Watering;
        ChkFertilizing.IsChecked = entry.Fertilizing || entry.Mulching;
        ChkPruning.IsChecked     = entry.Pruning;
        ChkPlanting.IsChecked    = entry.Planting;
        TxtNotes.Text            = entry.Notes;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (DpDate.SelectedDate == null)
        {
            MessageBox.Show("Date is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        bool fertMulch = ChkFertilizing.IsChecked == true;
        Entry.Date        = DpDate.SelectedDate.Value;
        Entry.Watering    = ChkWatering.IsChecked == true;
        Entry.Fertilizing = fertMulch;
        Entry.Mulching    = fertMulch;
        Entry.Pruning     = ChkPruning.IsChecked  == true;
        Entry.Planting    = ChkPlanting.IsChecked == true;
        Entry.Notes       = TxtNotes.Text.Trim();
        DialogResult = true;
    }
}
