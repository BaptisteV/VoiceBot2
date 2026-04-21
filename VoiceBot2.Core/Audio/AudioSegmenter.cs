using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public enum SegmentFlushReason { Silence, MaxDuration, StreamEnd }

public record AudioSegment(IList<AudioFrame> Frames, SegmentFlushReason Reason);

public class AudioSegmenter(ILogger<AudioSegmenter> logger) : IAudioSegmenter
{
    private readonly ILogger<AudioSegmenter> _logger = logger;
    public IObservable<AudioSegment> Segment(
        IObservable<AudioFrame> audioStream,
        TimeSpan silenceDuration,
        TimeSpan maxDuration)
    {
        return Observable.Create<AudioSegment>(observer =>
        {
            var state = new SegmentState(silenceDuration, maxDuration, _logger);

            return audioStream.Subscribe(
                onNext: frame =>
                {
                    var segment = state.Push(frame);
                    if (segment is not null)
                        observer.OnNext(segment);
                },
                onError: observer.OnError,
                onCompleted: () =>
                {
                    var remaining = state.Flush(SegmentFlushReason.StreamEnd);
                    if (remaining is not null)
                        observer.OnNext(remaining);
                    observer.OnCompleted();
                });
        });
    }
}
