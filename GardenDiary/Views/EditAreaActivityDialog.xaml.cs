using System.Windows;
using GardenDiary.Models;
using GardenDiary.ViewModels;

namespace GardenDiary.Views;

public partial class EditAreaActivityDialog : Window
{
    private readonly List<AreaCheckItem> _items;

    public IReadOnlyList<(Guid AreaId, bool IsChecked)> AreaResults =>
        _items.Select(i => (i.Area.Id, i.IsChecked)).ToList();

    public EditAreaActivityDialog(DateTime date, string activityName,
                                   IList<GardenArea> areas, HashSet<Guid> checkedAreaIds)
    {
        InitializeComponent();

        var def = MainViewModel.AreaActivityDefs.FirstOrDefault(d => d.Name == activityName);
        TxtActivityName.Text = activityName;
        TxtDate.Text         = date.ToString("dddd, MMMM d, yyyy");
        if (def != default)
        {
            HeaderBorder.Background    = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(def.Bg)!;
            TxtActivityName.Foreground = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(def.Fg)!;
        }

        _items = areas.OrderBy(a => a.Name).Select(a => new AreaCheckItem(a, checkedAreaIds.Contains(a.Id))).ToList();
        AreaCheckList.ItemsSource = _items;
    }

    private void OkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
