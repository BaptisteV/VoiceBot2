using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Audio;

public class AudioSegmenter(ILogger<AudioSegmenter> logger) : IAudioSegmenter
{
    private readonly ILogger<AudioSegmenter> _logger = logger;

    public IObservable<IList<AudioFrame>> Segment(
            IObservable<AudioFrame> audioStream,
            TimeSpan silenceDuration,
            TimeSpan maxDuration)
    {
        return Observable.Create<IList<AudioFrame>>(observer =>
        {
            var speechFrames = new List<AudioFrame>();       // frames to emit
            var silenceFrames = new List<AudioFrame>();      // trailing silence accumulator
            TimeSpan accumulatedSpeech = TimeSpan.Zero;
            TimeSpan accumulatedSilence = TimeSpan.Zero;

            void FlushSegment()
            {
                if (speechFrames.Count == 0 && silenceFrames.Count == 0) return;

                // Include trailing silence in the emitted segment
                var segment = speechFrames.Concat(silenceFrames).ToList();
                speechFrames.Clear();
                silenceFrames.Clear();
                accumulatedSpeech = TimeSpan.Zero;
                accumulatedSilence = TimeSpan.Zero;

                _logger.LogInformation("Emitting segment with {FrameCount} frames", segment.Count);
                observer.OnNext(segment);
            }

            return audioStream.Subscribe(
                onNext: frame =>
                {
                    var frameDuration = TimeSpan.FromSeconds(
                        frame.Buffer.Length / 2.0 / frame.SampleRate); // 16-bit = 2 bytes/sample

                    if (IsSilence(frame.Buffer))
                    {
                        if (speechFrames.Count == 0)
                        {
                            // Leading silence — discard, we haven't started a segment yet
                            return;
                        }

                        silenceFrames.Add(frame);
                        accumulatedSilence += frameDuration;

                        if (accumulatedSilence >= silenceDuration)
                        {
                            // Enough silence to close the segment
                            _logger.LogDebug("Silence threshold reached after {SilenceDuration:F2}s, flushing segment", accumulatedSilence.TotalSeconds);

                            FlushSegment();
                        }
                    }
                    else
                    {
                        // Log once on silence exit (i.e. silence was accumulating but speech resumed)
                        if (accumulatedSilence > TimeSpan.Zero)
                            _logger.LogDebug("Exiting silence after {SilenceDuration:F2}s, resuming segment", accumulatedSilence.TotalSeconds);

                        // Active speech — absorb any pending silence back into the segment
                        speechFrames.AddRange(silenceFrames);
                        accumulatedSpeech += accumulatedSilence; // count reclaimed silence as speech time
                        silenceFrames.Clear();
                        accumulatedSilence = TimeSpan.Zero;

                        speechFrames.Add(frame);
                        accumulatedSpeech += frameDuration;

                        if (accumulatedSpeech >= maxDuration)
                        {
                            // Hard cap reached — emit immediately and start fresh
                            _logger.LogInformation("maxDuration reached, forcing segment flush");
                            FlushSegment();
                        }
                    }
                },
                onError: observer.OnError,
                onCompleted: () =>
                {
                    // Flush whatever is left when the stream ends
                    FlushSegment();
                    observer.OnCompleted();
                });
        });
    }

    private static bool IsSilence(byte[] buffer, double threshold = 0.1)
    {
        var (_, rms) = ComputeVolume(buffer);
        if (rms < threshold)
            return true;
        return false;
    }
    private static (double Volume, double RmsVolume) ComputeVolume(byte[] pcmData)
    {
        int sampleCount = pcmData.Length / 2; // 16-bit = 2 bytes per sample

        double maxAmplitude = 0;
        double sumOfSquares = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            // Convert 2 bytes to a signed 16-bit sample (little-endian)
            short sample = BitConverter.ToInt16(pcmData, i * 2);

            // Normalize to [-1.0, 1.0]
            double normalized = sample / 32768.0;

            // Peak volume
            maxAmplitude = Math.Max(maxAmplitude, Math.Abs(normalized));

            // RMS accumulation
            sumOfSquares += normalized * normalized;
        }

        double rms = Math.Sqrt(sumOfSquares / sampleCount);

        return (maxAmplitude, rms);
    }
}
