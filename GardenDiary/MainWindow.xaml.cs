using System.Windows;
using System.Windows.Controls;
using GardenDiary.ViewModels;

namespace GardenDiary;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Cal_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Calendar cal)
            cal.SelectedDate = DateTime.Today;
    }

    private void Cal_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Calendar cal)
            vm.SelectedCalendarDate = cal.SelectedDate;
    }
}
