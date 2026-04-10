using Microsoft.Extensions.Logging;
using System.Reactive;
using System.Reactive.Linq;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Abstractions;

public class AudioSegmenter(ILogger<AudioSegmenter> logger) : IAudioSegmenter
{
    private readonly ILogger<AudioSegmenter> _logger = logger;

    public IObservable<IList<AudioFrame>> Segment(
        IObservable<AudioFrame> audioStream,
        TimeSpan silenceDuration,
        TimeSpan maxDuration)
    {
        var shared = audioStream.Publish().RefCount();

        var silenceSignal = shared
            .Select(f => AudioUtils.IsSilence(f.Buffer))
            .DistinctUntilChanged()
            .Where(s => s)
            .Throttle(silenceDuration)
            .Select(_ => Unit.Default);

        var timeoutSignal = shared
            .Sample(maxDuration)
            .Select(_ => Unit.Default);

        var flushSignal = silenceSignal.Merge(timeoutSignal);

        return shared
            .Buffer(flushSignal)
            .Where(chunk => chunk.Count > 0)
            .Do(chunk =>
                _logger.LogInformation("[SEGMENT] {ChunkCount} frames", chunk.Count));
    }
}
