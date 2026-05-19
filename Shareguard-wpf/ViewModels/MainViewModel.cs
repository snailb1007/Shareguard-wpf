using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShareGuard.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [RelayCommand]
    private void Loaded()
    {
        StatusMessage = "ShareGuard is ready to protect your privacy.";
    }
}
