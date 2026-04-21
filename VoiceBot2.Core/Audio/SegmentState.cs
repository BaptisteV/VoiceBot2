using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public sealed class SegmentState(TimeSpan silenceDuration, TimeSpan maxDuration, ILogger logger)
{
    private readonly List<(bool isSilent, AudioFrame)> _frames = [];

    /// <summary>Pushes a frame into the state machine. Returns a completed segment if one is ready.</summary>
    public AudioSegment? Push(AudioFrame frame)
    {
        var duration = FrameDuration(frame);
        var isSilent = frame.IsSilent();
        _frames.Add((isSilent, frame));
        return isSilent ? OnSilentFrame(duration) : OnSpeechFrame(duration);
    }

    public AudioSegment? Flush(SegmentFlushReason reason)
    {
        if (_frames.Count == 0)
        {
            return null;
        }

        logger.LogInformation("Emitting segment with {FrameCount} frames, reason={Reason}", _frames.Count, reason);
        var result = new AudioSegment(_frames.Select(f => f.Item2).ToList(), reason);
        _frames.Clear();
        return result;
    }

    private AudioSegment? OnSilentFrame(TimeSpan duration)
    {
        if (_frames.Count == 0)
            return null; // leading silence, discard

        var nFrame = (int)Math.Round(duration / silenceDuration);

        var silenceDurationHasElapsed = _frames.TakeLast(nFrame).Count(f => f.isSilent) == nFrame;

        if (silenceDurationHasElapsed)
        {
            logger.LogInformation("Silence threshold reached after {SilenceDuration}, flushing segment", silenceDuration);
            return Flush(SegmentFlushReason.Silence);
        }

        return null;
    }

    private AudioSegment? OnSpeechFrame(TimeSpan duration)
    {
        var accumulatedSpeech = _frames.Count(f => !f.isSilent) * duration;

        if (accumulatedSpeech >= maxDuration)
        {
            logger.LogInformation("maxDuration reached, forcing segment flush");
            return Flush(SegmentFlushReason.MaxDuration);
        }

        return null;
    }

    private static TimeSpan FrameDuration(AudioFrame frame) =>
        TimeSpan.FromSeconds(frame.Buffer.Length / 2.0 / frame.SampleRate);
}