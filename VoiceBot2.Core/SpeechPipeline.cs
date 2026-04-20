using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Commands;
using VoiceBot2.Core.Model;
using VoiceBot2.Core.SpeechToText;

namespace VoiceBot2.Core;

public sealed class SpeechPipeline(
    IAudioSource audio,
    ITranscribeService whisper,
    IAudioSegmenter audioSegmenter,
    ICommandDetector commandDetector,
    ICommandHandler commandHandler,
    ILogger<SpeechPipeline> logger) : ISpeechPipeline
{
    private readonly IAudioSource _audio = audio;
    private readonly ITranscribeService _whisper = whisper;
    private readonly IAudioSegmenter _audioSegmenter = audioSegmenter;
    private readonly ICommandDetector _commandDetector = commandDetector;
    private readonly ICommandHandler _commandHandler = commandHandler;
    private readonly ILogger<SpeechPipeline> _logger = logger;

    private CompositeDisposable _subscriptions = [];
    private readonly Subject<TimedResult> _timedResultSubject = new();
    public IObservable<TimedResult> Voice => _timedResultSubject.AsObservable();

    public void Start()
    {
        _logger.LogInformation("Pipeline start");

        var audioStream = _audio.AudioStream
            .Publish()
            .RefCount();

        var segments = _audioSegmenter.Segment(
            audioStream,
            silenceDuration: TimeSpan.FromMilliseconds(400),
            maxDuration: TimeSpan.FromSeconds(15));

        var timedSegments = segments
            .Select(chunk => new TimedChunk(chunk, DateTime.UtcNow));

        var textStream = timedSegments
            .ObserveOn(TaskPoolScheduler.Default)
            .Select(timedChunks =>
                Observable.FromAsync(async () =>
                {
                    var startProcessing = DateTime.UtcNow;

                    var queueDelay = startProcessing - timedChunks.EnqueuedAt;

                    var sw = Stopwatch.StartNew();

                    var result = await ProcessChunk(timedChunks.Chunks);

                    sw.Stop();

                    var endToEndLatency = DateTime.UtcNow - timedChunks.EnqueuedAt;
                    var r = new TimedResult(
                        result,
                        queueDelay,
                        sw.Elapsed,
                        endToEndLatency
                    );

                    _timedResultSubject.OnNext(r);
                    return r;
                })
            )
            .Merge(2)
            .Where(r =>
            {
                return r.Result.Text.TrimWithPunctuation().Length > 0;
            })
            .Publish()
            .RefCount();

        // 🔍 Command stream (NEW)
        var commandStream = _commandDetector.DetectCommands(textStream);
        var s = _commandHandler.Handlers(commandStream);

        // Subscriptions
        _subscriptions =
        [
            // Text logging stream
            textStream.Subscribe(x =>
            {
                _logger.LogInformation(
                    "STT={ProcessingTime}ms | TEXT={Text} | QUEUE={QueueDelay}ms | E2E={EndToEndLatency}ms",
                    x.ProcessingTime.TotalMilliseconds.ToString("F0"),
                    x.Result.Text,
                    x.QueueDelay.TotalMilliseconds.ToString("F0"),
                    x.EndToEndLatency.TotalMilliseconds.ToString("F0")
                );
            }, ex => _logger.LogError(ex, "TEXT STREAM ERROR")),

            commandStream.Subscribe(cmd =>
            {
                _logger.LogInformation("COMMAND DETECTED: {CommandText}", cmd.Source.Result.Text);
            }, ex => _logger.LogError(ex, "COMMAND STREAM ERROR")),

            s.Subscribe(async (h) =>
            {
                await h.Run;
                _logger.LogInformation("COMMAND HANDLED: {CommandText}", h.Result.Source.Result.Text);
            }, ex => _logger.LogError(ex, "COMMAND HANDLER ERROR")),
        ];
    }

    private async Task<TranscriptionResult> ProcessChunk(IList<AudioFrame> chunks)
    {
        var totalLength = chunks.Sum(f => f.Buffer.Length);
        _logger.LogInformation("STT Processing chunk ({ChunckCount} frames, {ChunksSize} bytes)", chunks.Count, totalLength);

        var buffer = new byte[totalLength];

        int offset = 0;
        foreach (var chuckBuffer in chunks.Select(c => c.Buffer))
        {
            Buffer.BlockCopy(chuckBuffer, 0, buffer, offset, chuckBuffer.Length);
            offset += chuckBuffer.Length;
        }

        var text = await _whisper.TranscribeAsync(buffer);

        return new TranscriptionResult(text, DateTime.UtcNow);
    }

    public void Stop()
    {
        _logger.LogInformation("Pipeline stopped");
        _subscriptions.Dispose();
        _subscriptions = [];
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptions.Dispose();
        _audio.Dispose();
        await _whisper.DisposeAsync();
    }

    public void LoadModel()
    {
        _whisper.Load(WhisperModels.ModelMedium, "fr");
        _logger.LogInformation("{TranscribeService} loaded", typeof(ITranscribeService).Name);
    }
}