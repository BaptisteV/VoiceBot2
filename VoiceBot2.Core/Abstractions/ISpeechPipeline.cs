using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Abstractions;

public interface ISpeechPipeline : IAsyncDisposable
{
    void LoadModel();
    void Start();
    void Stop();
    IObservable<TimedResult> Voice { get; }
}