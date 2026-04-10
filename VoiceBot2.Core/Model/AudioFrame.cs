
namespace VoiceBot2.Core.Model;

public record AudioFrame(byte[] Buffer, int SampleRate, DateTime Timestamp);
