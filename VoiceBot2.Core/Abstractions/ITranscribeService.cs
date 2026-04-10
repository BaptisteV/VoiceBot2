namespace VoiceBot2.Core.Abstractions;

public interface ITranscribeService : IDisposable
{
    void Load(string modelPath, string language);
    Task<string> TranscribeAsync(byte[] pcmData);
}