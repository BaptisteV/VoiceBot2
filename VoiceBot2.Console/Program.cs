using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceBot2.Core;
using VoiceBot2.Core.Abstractions;
using Whisper.net.Logger;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddServices();
    })
    .ConfigureLogging(l =>
    {
        LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
        l.AddSimpleConsole(s =>
        {
            s.IncludeScopes = false;
            s.SingleLine = true;
            s.TimestampFormat = "[HH:mm:ss:fff] ";
        })
        .AddDebug()
        .SetMinimumLevel(LogLevel.Debug);
    })
    .Build();

using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<ISpeechPipeline>();
var audio = scope.ServiceProvider.GetRequiredService<IAudioSource>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();


var lifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("Starting...");
    audio.Start();
    pipeline.Start();
    logger.LogInformation("Started");
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Stopping");
    pipeline.Stop();
    audio.Stop();
    logger.LogInformation("Stopped");
});

await host.RunAsync();
