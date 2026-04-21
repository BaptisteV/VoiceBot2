
using Whisper.net;

namespace VoiceBot2.Core.Model;

public record TranscriptionResult(List<SegmentData> Segments, DateTime Timestamp);

public static class SegmentDataListExtensions
{
    extension(List<SegmentData> segments)
    {
        public string ConcatenatedText => string.Concat(segments.SelectMany(s => s.Text));
    }
}