using System.Windows;
using GardenDiary.Models;
using WinForms = System.Windows.Forms;

namespace GardenDiary.Views;

public partial class BackupSettingsDialog : Window
{
    public string SelectedPath { get; private set; }

    public BackupSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        SelectedPath = settings.BackupFolderPath;
        TxtPath.Text = SelectedPath;

        TxtLastBackup.Text = settings.LastAutoBackupDate.HasValue
            ? $"Last automatic backup: {settings.LastAutoBackupDate:yyyy-MM-dd}"
            : "No automatic backup has run yet.";
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select backup folder",
            UseDescriptionForTitle = true,
            SelectedPath = TxtPath.Text
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
