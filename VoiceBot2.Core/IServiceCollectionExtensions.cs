using Microsoft.Extensions.DependencyInjection;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.SpeechToText;

namespace VoiceBot2.Core;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddServices()
        {
            return services
                .AddSingleton<IAudioSource, NAudioSource>()
                .AddSingleton<ITranscribeService, WhisperService>()
                .AddTransient<IAudioSegmenter, AudioSegmenter>()
                .AddSingleton<ISpeechPipeline, SpeechPipeline>();
        }
    }
}
