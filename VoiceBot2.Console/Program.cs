using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceBot2.Core;
using VoiceBot2.Core.Abstractions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddServices();
    })
    .ConfigureLogging(l =>
    {
        l.AddSimpleConsole(s =>
        {
            s.IncludeScopes = false;
            s.SingleLine = true;
            s.TimestampFormat = "[HH:mm:ss:fff] ";
        })
        .AddDebug()
        .SetMinimumLevel(LogLevel.Trace);
    })
    .Build();

using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<ISpeechPipeline>();
var audio = scope.ServiceProvider.GetRequiredService<IAudioSource>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

audio.Start();
pipeline.Start();

logger.LogInformation("Press ENTER to stop...");
Console.ReadLine();

pipeline.Stop();
audio.Stop();