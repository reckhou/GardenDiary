using System.Windows;
using System.Windows.Controls;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class GeneralActivityDialog : Window
{
    private List<PlantCheckItem> _allItems = new();
    private List<PlantGroup>     _allGroups = new();

    public GeneralDiaryEntry Entry { get; private set; }

    public GeneralActivityDialog(GeneralDiaryEntry entry, IList<Plant> plants, IList<GardenArea> areas)
    {
        InitializeComponent();
        Entry = entry;

        DtpDate.SelectedDate = entry.Date;
        TxtNotes.Text        = entry.Notes;

        var preselected = entry.PlantIds.ToHashSet();

        _allItems = plants
            .OrderBy(p => p.CommonName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlantCheckItem(p, preselected.Contains(p.Id)))
            .ToList();

        var placedIds = areas
            .SelectMany(a => a.PlantPlacements.Select(pp => pp.PlantId))
            .ToHashSet();

        _allGroups = new List<PlantGroup>();

        foreach (var area in areas.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var areaPlantIds = area.PlantPlacements.Select(pp => pp.PlantId).ToHashSet();
            var items = _allItems.Where(ci => areaPlantIds.Contains(ci.Plant.Id)).ToList();
            if (items.Count > 0)
                _allGroups.Add(new PlantGroup { GroupName = area.Name, Items = items });
        }

        var notPlaced = _allItems.Where(ci => !placedIds.Contains(ci.Plant.Id)).ToList();
        if (notPlaced.Count > 0)
            _allGroups.Add(new PlantGroup { GroupName = "Not Placed", Items = notPlaced });

        if (_allGroups.Count == 0 && _allItems.Count > 0)
            _allGroups.Add(new PlantGroup { GroupName = "All Plants", Items = _allItems });

        ApplyFilter("");
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(TxtSearch.Text);

    private void ApplyFilter(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query))
        {
            PlantGroupList.ItemsSource = _allGroups;
            return;
        }

        var filtered = _allGroups
            .Select(g => new PlantGroup
            {
                GroupName = g.GroupName,
                Items = g.Items.Where(ci =>
                    ci.Plant.CommonName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    (ci.Plant.Variety   ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    (ci.Plant.LatinName ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase)
                ).ToList()
            })
            .Where(g => g.Items.Count > 0)
            .ToList();

        PlantGroupList.ItemsSource = filtered;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var ci in _allItems)
            ci.IsChecked = false;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (DtpDate.SelectedDate == null)
        {
            MessageBox.Show("Please select a date.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Entry = new GeneralDiaryEntry
        {
            Id       = Entry.Id,
            Date     = DtpDate.SelectedDate.Value.Date,
            Notes    = TxtNotes.Text.Trim(),
            PlantIds = _allItems.Where(ci => ci.IsChecked).Select(ci => ci.Plant.Id).ToList()
        };

        DialogResult = true;
    }
}
