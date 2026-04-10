using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Abstractions;

public interface IAudioSource : IDisposable
{
    IObservable<AudioFrame> AudioStream { get; }

    void Start();
    void Stop();
}
