using Whisper.net;

namespace VoiceBot2.Core.Abstractions;

public interface ITranscribeService : IAsyncDisposable
{
    void Load(string modelPath, string language);
    Task<List<SegmentData>> TranscribeAsync(byte[] pcmData);
}