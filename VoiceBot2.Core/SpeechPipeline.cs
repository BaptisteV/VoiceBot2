using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.Model;
using VoiceBot2.Core.SpeechToText;

namespace VoiceBot2.Core;

public class SpeechPipeline : IDisposable, ISpeechPipeline
{
    private readonly NAudioSource _audio;
    private readonly WhisperService _whisper;

    private IDisposable _subscription;

    public SpeechPipeline(NAudioSource audio, WhisperService whisper)
    {
        _audio = audio;
        _whisper = whisper;
    }

    public void Start()
    {
        Console.WriteLine("Pipeline started");

        var audioStream = _audio.AudioStream
            .Publish()
            .RefCount();

        var segments = AudioSegmentation.Segment(
            audioStream,
            silenceDuration: TimeSpan.FromMilliseconds(400),
            maxDuration: TimeSpan.FromSeconds(4),
            log: Console.WriteLine);

        var timedSegments = segments
            .Select(chunk => new TimedChunk(chunk, DateTime.UtcNow));

        var textStream = timedSegments
            .ObserveOn(TaskPoolScheduler.Default)
            .Select(timedChunk =>
                Observable.FromAsync(async () =>
                {
                    var startProcessing = DateTime.UtcNow;

                    var queueDelay = startProcessing - timedChunk.EnqueuedAt;

                    var sw = Stopwatch.StartNew();

                    var result = await ProcessChunk(timedChunk.Chunk);

                    sw.Stop();

                    var endToEndLatency = DateTime.UtcNow - timedChunk.EnqueuedAt;

                    return new TimedResult(
                        result,
                        queueDelay,
                        sw.Elapsed,
                        endToEndLatency
                    );
                })
            )
            .Merge(2);

        _subscription = textStream.Subscribe(
            x =>
            {
                Console.WriteLine(
                    $"[TEXT] {x.Result.Text} | " +
                    $"Queue={x.QueueDelay.TotalMilliseconds:F0}ms | " +
                    $"STT={x.ProcessingTime.TotalMilliseconds:F0}ms | " +
                    $"E2E={x.EndToEndLatency.TotalMilliseconds:F0}ms"
                );
            },
            ex => Console.WriteLine($"[ERROR] {ex}")
        );
    }

    private async Task<TranscriptionResult> ProcessChunk(IList<AudioFrame> chunk)
    {
        Console.WriteLine($"[STT] Processing chunk ({chunk.Count} frames)");

        var totalLength = chunk.Sum(f => f.Buffer.Length);
        var buffer = new byte[totalLength];

        int offset = 0;
        foreach (var f in chunk)
        {
            Buffer.BlockCopy(f.Buffer, 0, buffer, offset, f.Buffer.Length);
            offset += f.Buffer.Length;
        }

        var text = await _whisper.TranscribeAsync(buffer);

        return new TranscriptionResult(text, DateTime.UtcNow);
    }

    public void Stop()
    {
        Console.WriteLine("Pipeline stopped");
        _subscription?.Dispose();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _audio.Dispose();
        _whisper.Dispose();
    }
}