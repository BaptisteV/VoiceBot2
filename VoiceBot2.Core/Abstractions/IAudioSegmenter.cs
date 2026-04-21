using VoiceBot2.Core.Audio;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Abstractions;

public interface IAudioSegmenter
{
    IObservable<AudioSegment> Segment(IObservable<AudioFrame> audioStream, TimeSpan silenceDuration, TimeSpan maxDuration);
}