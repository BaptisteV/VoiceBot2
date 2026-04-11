using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;

namespace VoiceBot2.Maui;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string IconPath { get; set; } = "on.ico";

    private bool IsWindowVisible { get; set; } = true;
    [RelayCommand]
    public void ShowHideWindow()
    {
        var window = Application.Current?.Windows[0];
        if (window == null)
        {
            return;
        }

        if (IsWindowVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IconPath = "off.ico";
                window.Hide();
            });
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IconPath = "on.ico";
                window.Show();
            });
        }
        IsWindowVisible = !IsWindowVisible;
    }

    [RelayCommand]
    public static void ExitApplication()
    {
        Application.Current?.Quit();
    }
}
