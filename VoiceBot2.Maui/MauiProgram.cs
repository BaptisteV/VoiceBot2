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

        builder.ConfigureLifecycleEvents(events =>
        {
#if WINDOWS
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    SetupHotkeys();
                });
            });
#endif
        });

        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        var services = builder.Services;
        services.AddServices();

        return builder.Build();
    }

    private static void SetupHotkeys()
    {
        var hotkeyManager = new HotkeyManager();

        hotkeyManager.HotkeyPressed += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Shell.Current?.CurrentPage is MainPage page)
                {
                    page.ShowHideWindowCommand.Execute(null);
                }
            });
        };

        hotkeyManager.HotkeyReleased += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Shell.Current?.CurrentPage is MainPage page)
                {
                    page.ShowHideWindowCommand.Execute(null);
                }
            });
        };

        hotkeyManager.Register();
    }
}
