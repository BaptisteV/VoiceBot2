using NAudio.Wave;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public sealed class NAudioSource : IAudioSource
{
    private readonly WaveInEvent _waveIn;

    private readonly ISubject<AudioFrame> _subject =
        Subject.Synchronize(new Subject<AudioFrame>());

    public IObservable<AudioFrame> AudioStream => _subject;

    public NAudioSource()
    {
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 20
        };

        _waveIn.DataAvailable += (s, e) =>
        {
            var buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

            _subject.OnNext(new AudioFrame(
                buffer,
                16000,
                DateTime.UtcNow));
        };
    }

    public void Start() => _waveIn.StartRecording();
    public void Stop() => _waveIn.StopRecording();

    public void Dispose()
    {
        _waveIn.Dispose();
        _subject.OnCompleted();
        //_subject.Dispose();
    }
}