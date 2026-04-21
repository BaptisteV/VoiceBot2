using System.Reactive.Linq;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.Model;
using Whisper.net;

namespace VoiceBot2.Core;

public static class BoundaryStitcher
{
    private static readonly TimeSpan BoundaryThreshold = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Prepends any carried-over tail bytes from a previous forced flush,
    /// then checks the Whisper result to decide whether to carry bytes into the next chunk.
    /// </summary>
    public static IObservable<(byte[] Pcm, AudioSegment Segment)> StitchBoundaries(
        this IObservable<AudioSegment> segments,
        Func<byte[]> getCarryOver)
    {
        return segments.Select(segment =>
        {
            var pcm = BuildPcm(getCarryOver(), segment.Frames);
            return (pcm, segment);
        });
    }
    /// <summary>
    /// After transcription, checks if a forced flush cut mid-word and extracts tail bytes to carry over.
    /// </summary>
    public static byte[] ExtractCarryOver(byte[] pcm, List<SegmentData> segments, SegmentFlushReason reason)
    {
        if (reason == SegmentFlushReason.Silence || reason == SegmentFlushReason.StreamEnd)
            return Array.Empty<byte>();

        if (segments.Count == 0)
            return Array.Empty<byte>();

        var lastSegment = segments[^1];
        var totalDuration = PcmDuration(pcm);
        var uncoveredTail = totalDuration - lastSegment.End;

        if (uncoveredTail < BoundaryThreshold)
            return Array.Empty<byte>(); // Whisper covered to the end, clean cut

        // Extract the bytes Whisper didn't confidently cover
        var carryStart = TimeSpanToByteOffset(lastSegment.End, sampleRate: 16000);
        var carryBytes = new byte[pcm.Length - carryStart];
        Buffer.BlockCopy(pcm, carryStart, carryBytes, 0, carryBytes.Length);

        return carryBytes;
    }

    private static byte[] BuildPcm(byte[] carryOver, IList<AudioFrame> frames)
    {
        var frameBytes = frames.SelectMany(f => f.Buffer).ToArray();
        if (carryOver.Length == 0) return frameBytes;

        var merged = new byte[carryOver.Length + frameBytes.Length];
        Buffer.BlockCopy(carryOver, 0, merged, 0, carryOver.Length);
        Buffer.BlockCopy(frameBytes, 0, merged, carryOver.Length, frameBytes.Length);
        return merged;
    }

    private static TimeSpan PcmDuration(byte[] pcm) =>
        TimeSpan.FromSeconds(pcm.Length / 2.0 / 16000); // 16-bit, 16kHz mono

    private static int TimeSpanToByteOffset(TimeSpan time, int sampleRate) =>
        (int)(time.TotalSeconds * sampleRate) * 2; // * 2 for 16-bit
}