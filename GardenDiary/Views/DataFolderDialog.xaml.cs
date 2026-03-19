using System.Windows;
using WinForms = System.Windows.Forms;

namespace GardenDiary.Views;

public partial class DataFolderDialog : Window
{
    public string SelectedPath { get; private set; }

    public DataFolderDialog(string currentPath)
    {
        InitializeComponent();
        SelectedPath = currentPath;
        TxtPath.Text = currentPath;
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description        = "Select folder to store Garden Diary data",
            UseDescriptionForTitle = true,
            SelectedPath       = TxtPath.Text
        };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            TxtPath.Text = dialog.SelectedPath;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        SelectedPath = TxtPath.Text.Trim();
        DialogResult = true;
    }
}
