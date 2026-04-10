namespace VoiceBot2.Core.Abstractions;

public interface ISpeechPipeline : IAsyncDisposable
{
    void Start();
    void Stop();
}