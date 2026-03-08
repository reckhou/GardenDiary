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
        ChkPlanting.IsChecked    = entry.Planting;
        ChkWatering.IsChecked    = entry.Watering;
        ChkFertilizing.IsChecked = entry.Fertilizing;
        ChkWeeding.IsChecked     = entry.Weeding;
        ChkMulching.IsChecked    = entry.Mulching;
        ChkPruning.IsChecked     = entry.Pruning;
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
        Entry.Date        = DpDate.SelectedDate.Value;
        Entry.Planting    = ChkPlanting.IsChecked    == true;
        Entry.Watering    = ChkWatering.IsChecked    == true;
        Entry.Fertilizing = ChkFertilizing.IsChecked == true;
        Entry.Weeding     = ChkWeeding.IsChecked     == true;
        Entry.Mulching    = ChkMulching.IsChecked    == true;
        Entry.Pruning     = ChkPruning.IsChecked     == true;
        Entry.Notes       = TxtNotes.Text.Trim();
        DialogResult = true;
    }
}
