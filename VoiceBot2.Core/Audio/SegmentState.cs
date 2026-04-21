using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public sealed class SegmentState(TimeSpan silenceDuration, TimeSpan maxDuration, ILogger logger)
{
    private readonly List<AudioFrame> _speechFrames = [];
    private readonly List<AudioFrame> _silenceFrames = [];
    private TimeSpan _accumulatedSpeech = TimeSpan.Zero;
    private TimeSpan _accumulatedSilence = TimeSpan.Zero;

    /// <summary>Pushes a frame into the state machine. Returns a completed segment if one is ready.</summary>
    public IList<AudioFrame>? Push(AudioFrame frame)
    {
        var duration = FrameDuration(frame);
        return IsSilence(frame.Buffer) ? OnSilentFrame(frame, duration) : OnSpeechFrame(frame, duration);
    }

    public IList<AudioFrame>? Flush()
    {
        if (_speechFrames.Count == 0)
        {
            _silenceFrames.Clear();
            _accumulatedSilence = TimeSpan.Zero;
            return null;
        }

        var segment = _speechFrames.Concat(_silenceFrames).ToList();
        Reset();
        logger.LogInformation("Emitting segment with {FrameCount} frames", segment.Count);
        return segment;
    }

    private IList<AudioFrame>? OnSilentFrame(AudioFrame frame, TimeSpan duration)
    {
        if (_speechFrames.Count == 0)
            return null; // leading silence, discard

        if (_accumulatedSilence == TimeSpan.Zero)
            logger.LogDebug("Entering silence");

        _silenceFrames.Add(frame);
        _accumulatedSilence += duration;

        if (_accumulatedSilence >= silenceDuration)
        {
            logger.LogInformation("Silence threshold reached after {SilenceDuration:F2}s, flushing segment", _accumulatedSilence.TotalSeconds);
            return Flush();
        }

        return null;
    }

    private IList<AudioFrame>? OnSpeechFrame(AudioFrame frame, TimeSpan duration)
    {
        if (_accumulatedSilence > TimeSpan.Zero)
            logger.LogInformation("Exiting silence after {SilenceDuration:F2}s, resuming segment", _accumulatedSilence.TotalSeconds);

        AbsorbPendingSilence();

        _speechFrames.Add(frame);
        _accumulatedSpeech += duration;

        if (_accumulatedSpeech >= maxDuration)
        {
            logger.LogInformation("maxDuration reached, forcing segment flush");
            return Flush();
        }

        return null;
    }

    private void AbsorbPendingSilence()
    {
        _speechFrames.AddRange(_silenceFrames);
        _accumulatedSpeech += _accumulatedSilence;
        _silenceFrames.Clear();
        _accumulatedSilence = TimeSpan.Zero;
    }

    private void Reset()
    {
        _speechFrames.Clear();
        _silenceFrames.Clear();
        _accumulatedSpeech = TimeSpan.Zero;
        _accumulatedSilence = TimeSpan.Zero;
    }

    private static TimeSpan FrameDuration(AudioFrame frame) =>
        TimeSpan.FromSeconds(frame.Buffer.Length / 2.0 / frame.SampleRate);

    private static bool IsSilence(byte[] buffer, double threshold = 0.075)
    {
        var (_, rms) = ComputeVolume(buffer);
        if (rms < threshold)
            return true;
        return false;
    }

    private static (double Volume, double RmsVolume) ComputeVolume(byte[] pcmData)
    {
        int sampleCount = pcmData.Length / 2; // 16-bit = 2 bytes per sample

        double maxAmplitude = 0;
        double sumOfSquares = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            // Convert 2 bytes to a signed 16-bit sample (little-endian)
            short sample = BitConverter.ToInt16(pcmData, i * 2);

            // Normalize to [-1.0, 1.0]
            double normalized = sample / 32768.0;

            // Peak volume
            maxAmplitude = Math.Max(maxAmplitude, Math.Abs(normalized));

            // RMS accumulation
            sumOfSquares += normalized * normalized;
        }

        double rms = Math.Sqrt(sumOfSquares / sampleCount);

        return (maxAmplitude, rms);
    }
}