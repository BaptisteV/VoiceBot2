public record AudioFrame(byte[] Buffer, int SampleRate, DateTime Timestamp);

public record TranscriptionResult(string Text, DateTime Timestamp);

public record TimedChunk(IList<AudioFrame> Chunk, DateTime EnqueuedAt);

public record TimedResult(TranscriptionResult Result, TimeSpan QueueDelay, TimeSpan ProcessingTime, TimeSpan EndToEndLatency);