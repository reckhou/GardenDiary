using System.Windows;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public class AreaCheckItem
{
    public GardenArea Area { get; }
    public bool IsChecked { get; set; }
    public AreaCheckItem(GardenArea area, bool isChecked = false) { Area = area; IsChecked = isChecked; }
}

public partial class AreaCalendarEntryDialog : Window
{
    private readonly List<AreaCheckItem> _items;

    public List<GardenArea> SelectedAreas { get; private set; } = new();
    public AreaDiaryEntry Entry { get; private set; } = new();

    public AreaCalendarEntryDialog(DateTime date, IList<GardenArea> areas)
    {
        InitializeComponent();
        DtpDate.SelectedDate = date.Date;
        _items = areas.OrderBy(a => a.Name).Select(a => new AreaCheckItem(a)).ToList();
        AreaCheckList.ItemsSource = _items;
    }

    // Edit-mode constructor: pre-fills for a single area's existing entry
    public AreaCalendarEntryDialog(DateTime date, GardenArea editArea, AreaDiaryEntry? existing, IList<GardenArea> areas)
    {
        InitializeComponent();
        DtpDate.SelectedDate = date.Date;
        _items = areas.OrderBy(a => a.Name).Select(a => new AreaCheckItem(a, a.Id == editArea.Id)).ToList();
        AreaCheckList.ItemsSource = _items;
        if (existing != null)
        {
            ChkMowing.IsChecked      = existing.Mowing;
            ChkWatering.IsChecked    = existing.Watering;
            ChkOverseeding.IsChecked = existing.Overseeding;
            ChkFeeding.IsChecked     = existing.Feeding;
            ChkAerating.IsChecked    = existing.Aerating;
            TxtNotes.Text            = existing.Notes ?? "";
        }
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (DtpDate.SelectedDate == null)
        {
            MessageBox.Show("Please select a date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SelectedAreas = _items.Where(i => i.IsChecked).Select(i => i.Area).ToList();
        if (SelectedAreas.Count == 0)
        {
            MessageBox.Show("Please select at least one area.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        bool anyActivity = ChkMowing.IsChecked == true || ChkWatering.IsChecked == true ||
                           ChkOverseeding.IsChecked == true || ChkFeeding.IsChecked == true ||
                           ChkAerating.IsChecked == true;
        if (!anyActivity)
        {
            MessageBox.Show("Please select at least one activity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Entry = new AreaDiaryEntry
        {
            Date        = DtpDate.SelectedDate.Value.Date,
            Mowing      = ChkMowing.IsChecked    == true,
            Watering    = ChkWatering.IsChecked   == true,
            Overseeding = ChkOverseeding.IsChecked == true,
            Feeding     = ChkFeeding.IsChecked    == true,
            Aerating    = ChkAerating.IsChecked   == true,
            Notes       = TxtNotes.Text.Trim()
        };
        DialogResult = true;
    }
}
