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
            var speechFrames = new List<AudioFrame>();
            var silenceFrames = new List<AudioFrame>();
            TimeSpan accumulatedSpeech = TimeSpan.Zero;
            TimeSpan accumulatedSilence = TimeSpan.Zero;

            void FlushSegment()
            {
                // Don't emit if there's no speech content, just discard accumulated silence
                if (speechFrames.Count == 0)
                {
                    silenceFrames.Clear();
                    accumulatedSilence = TimeSpan.Zero;
                    return;
                }

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
                        frame.Buffer.Length / 2.0 / frame.SampleRate);

                    if (IsSilence(frame.Buffer))
                    {
                        if (speechFrames.Count == 0)
                            return; // leading silence, discard

                        if (accumulatedSilence == TimeSpan.Zero)
                            _logger.LogDebug("Entering silence");

                        silenceFrames.Add(frame);
                        accumulatedSilence += frameDuration;

                        if (accumulatedSilence >= silenceDuration)
                        {
                            _logger.LogInformation("Silence threshold reached after {SilenceDuration:F2}s, flushing segment", accumulatedSilence.TotalSeconds);
                            FlushSegment();
                        }
                    }
                    else
                    {
                        if (accumulatedSilence > TimeSpan.Zero)
                            _logger.LogInformation("Exiting silence after {SilenceDuration:F2}s, resuming segment", accumulatedSilence.TotalSeconds);

                        // Absorb pending silence back into speech
                        speechFrames.AddRange(silenceFrames);
                        accumulatedSpeech += accumulatedSilence;
                        silenceFrames.Clear();
                        accumulatedSilence = TimeSpan.Zero;

                        speechFrames.Add(frame);
                        accumulatedSpeech += frameDuration;

                        if (accumulatedSpeech >= maxDuration)
                        {
                            _logger.LogInformation("maxDuration reached, forcing segment flush");
                            FlushSegment();
                        }
                    }
                },
                onError: observer.OnError,
                onCompleted: () =>
                {
                    FlushSegment();
                    observer.OnCompleted();
                });
        });
    }

    private static bool IsSilence(byte[] buffer, double threshold = 0.075)
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
