using System.Windows;
using ShareGuard.App.ViewModels;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Receives its ViewModel via constructor injection.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
