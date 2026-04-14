
namespace VoiceBot2.Core.Model;

public record TimedChunk(IList<AudioFrame> Chunks, DateTime EnqueuedAt);
