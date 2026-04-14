using Microsoft.Extensions.Logging;
using VoiceBot2.Core;

#if WINDOWS
using VoiceBot2.Maui.Platforms.Windows;
using Whisper.net.Logger;
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
        LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddSingleton<HotkeyManager>();

        var services = builder.Services;
        services.AddServices();
        var h = builder.Services.BuildServiceProvider();
        var wLogger = h.GetRequiredService<ILogger<object>>();
        LogProvider.AddLogger((l, a) =>
        {
            wLogger.Log((LogLevel)l, a);
        });
        /*builder.ConfigureLifecycleEvents(events =>
        {
            var h = builder.Services.BuildServiceProvider();
            var vm = h.GetRequiredService<MainPageViewModel>();
            var hkm = h.GetRequiredService<HotkeyManager>();
#if WINDOWS
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    SetupHotkeys(vm, hkm);
                });
            });
#endif
        });*/

        return builder.Build();
    }

    private static void SetupHotkeys(MainPageViewModel vm, HotkeyManager hotkeyManager)
    {
        hotkeyManager.HotkeyPressed += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                vm.Start();
            });
        };

        hotkeyManager.HotkeyReleased += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                vm.Stop();
            });
        };

        hotkeyManager.Register();
    }
}
