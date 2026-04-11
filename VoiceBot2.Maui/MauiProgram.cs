using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using VoiceBot2.Core;
#if WINDOWS
using VoiceBot2.Maui.Platforms.Windows;
#endif


namespace VoiceBot2.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        var vm = new MainPageViewModel();
        builder.Services.AddSingleton(vm);

        var services = builder.Services;
        services.AddServices();
        builder.ConfigureLifecycleEvents(events =>
        {
#if WINDOWS
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    SetupHotkeys(vm);
                });
            });
#endif
        });

        return builder.Build();
    }

    private static void SetupHotkeys(MainPageViewModel vm)
    {
        var hotkeyManager = new HotkeyManager();

        hotkeyManager.HotkeyPressed += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                vm.ShowHideWindowCommand.Execute(null);
            });
        };

        hotkeyManager.HotkeyReleased += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                vm.ShowHideWindowCommand.Execute(null);
            });
        };

        hotkeyManager.Register();
    }
}
