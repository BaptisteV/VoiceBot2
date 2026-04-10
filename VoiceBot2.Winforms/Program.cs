using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceBot2.Win.Voices;

namespace VoiceBot2.Win;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.ConfigureServices((services) =>
        {
            services.AddLogging(c =>
            {
                c.AddSimpleConsole(s =>
                {
                    s.IncludeScopes = false;
                    s.SingleLine = true;
                    s.TimestampFormat = "[HH:mm:ss:fff] ";
                }).AddDebug().SetMinimumLevel(LogLevel.Trace);
            });

            services.AddSingleton<IVoiceListener, ReactiveVoiceListener>();
            services.AddSingleton<IMicRecorder, NAudioMicRecorder>();
        });

        var host = hostBuilder.Build();
        Application.Run(new Form1(
            host.Services.GetRequiredService<IVoiceListener>(),
            host.Services.GetRequiredService<ILogger<Form1>>()));
    }
}