using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public sealed class NAudioSource : IAudioSource
{
    private readonly WaveInEvent _waveIn;

    private readonly ISubject<AudioFrame> _subject = Subject.Synchronize(new Subject<AudioFrame>());
    private readonly ILogger<NAudioSource> _logger;
    public IObservable<AudioFrame> AudioStream => _subject;

    public NAudioSource(ILogger<NAudioSource> logger)
    {
        _logger = logger;
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
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

    public void Start()
    {
        _waveIn.StartRecording();
        _logger.LogInformation("Audio recording started");
    }

    public void Stop()
    {
        _waveIn.StopRecording();
        _logger.LogInformation("Audio recording stopped");
    }

    public void Dispose()
    {
        _subject.OnCompleted();
        _waveIn.Dispose();
    }
}