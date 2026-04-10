namespace VoiceBot2.Core.Abstractions;

public interface ITranscribeService : IDisposable
{
    Task<string> TranscribeAsync(byte[]
    pcmData);
}