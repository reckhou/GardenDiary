using System.Windows;
using GardenDiary.ViewModels;

namespace GardenDiary;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        var vm = (MainViewModel)window.DataContext;
        MainWindow = window;
        window.Show();

        // Run auto-backup after the window is visible
        vm.RunAutoBackupIfDue();
    }
}
