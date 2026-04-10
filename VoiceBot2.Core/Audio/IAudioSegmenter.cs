using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public interface IAudioSegmenter
{
    IObservable<IList<AudioFrame>> Segment(IObservable<AudioFrame> audioStream, TimeSpan silenceDuration, TimeSpan maxDuration);
}