namespace VoiceBot2.Core.Abstractions;

public interface ITranscribeService : IAsyncDisposable
{
    void Load(string modelPath, string language);
    Task<string> TranscribeAsync(byte[] pcmData);
}