
namespace VoiceBot2.Core.Model;

public record TimedResult(TranscriptionResult Result, TimeSpan QueueDelay, TimeSpan ProcessingTime, TimeSpan EndToEndLatency);