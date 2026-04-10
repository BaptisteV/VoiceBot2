namespace VoiceBot2.Core.Abstractions;

public interface ISpeechPipeline : IDisposable
{
    void Start();
    void Stop();
}