using System.Windows;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class PlantEditDialog : Window
{
    public Plant Plant { get; private set; }

    public PlantEditDialog(Plant plant)
    {
        InitializeComponent();
        Plant = plant;
        TxtCommonName.Text = plant.CommonName;
        TxtLatinName.Text = plant.LatinName;
        TxtVariety.Text = plant.Variety;
        Loaded += (_, _) => TxtCommonName.Focus();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCommonName.Text))
        {
            MessageBox.Show("Common Name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCommonName.Focus();
            return;
        }
        Plant.CommonName = TxtCommonName.Text.Trim();
        Plant.LatinName = TxtLatinName.Text.Trim();
        Plant.Variety = TxtVariety.Text.Trim();
        DialogResult = true;
    }
}
