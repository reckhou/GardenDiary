using System.Windows;
using GardenDiary.Models;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class GardenAreaEditDialog : Window
{
    public GardenArea Area { get; private set; }

    public GardenAreaEditDialog(GardenArea area)
    {
        InitializeComponent();
        Area = area;
        TxtName.Text   = area.Name;
        TxtWidth.Text  = area.Width.ToString();
        TxtHeight.Text = area.Height.ToString();
        Loaded += (_, _) => TxtName.Focus();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }
        if (!double.TryParse(TxtWidth.Text, out var w) || w <= 0)
        {
            MessageBox.Show("Width must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtWidth.Focus();
            return;
        }
        if (!double.TryParse(TxtHeight.Text, out var h) || h <= 0)
        {
            MessageBox.Show("Height must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtHeight.Focus();
            return;
        }
        Area.Name   = TxtName.Text.Trim();
        Area.Width  = w;
        Area.Height = h;
        DialogResult = true;
    }
}
