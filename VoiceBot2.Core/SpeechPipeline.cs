using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;
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
        _audio.Start();
        _logger.LogInformation("Pipeline start");
        var audioStream = _audio.AudioStream
            .Publish()
            .RefCount();

        var segments = _audioSegmenter.Segment(
                audioStream,
                silenceDuration: TimeSpan.FromMilliseconds(400),
                maxDuration: TimeSpan.FromSeconds(5))
            .Scan(
                seed: (Buffer: new List<AudioFrame>(), Ready: (IList<AudioFrame>?)null),
                accumulator: (state, segment) =>
                {
                    var merged = state.Buffer.Concat(segment).ToList();
                    if (merged.Count >= 50)
                        return (Buffer: new List<AudioFrame>(), Ready: merged);
                    else
                        return (Buffer: merged, Ready: null);
                })
            .Where(state => state.Ready is not null)
            .Select(state => state.Ready!);

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
            .Publish()
            .RefCount();

        var commandStream = _commandDetector.DetectCommands(textStream);
        var s = _commandHandler.Handlers(commandStream);

        _subscriptions =
        [
            textStream.Subscribe(x =>
            {
                _logger.LogInformation(
                    "STT={ProcessingTime}ms | TEXT={Text} | QUEUE={QueueDelay}ms | E2E={EndToEndLatency}ms",
                    x.ProcessingTime.TotalMilliseconds.ToString("F0"),
                    x.Result.Segments.ConcatenatedText,
                    x.QueueDelay.TotalMilliseconds.ToString("F0"),
                    x.EndToEndLatency.TotalMilliseconds.ToString("F0")
                );
            }, ex => _logger.LogError(ex, "TEXT STREAM ERROR")),

            commandStream.Subscribe(cmd =>
            {
                _logger.LogInformation("COMMAND DETECTED: {CommandText}", cmd.Source.Result.Segments);
            }, ex => _logger.LogError(ex, "COMMAND STREAM ERROR")),

            s.Subscribe(async h =>
            {
                await h.Run;
                _logger.LogInformation("COMMAND HANDLED: {CommandText}", h.Result.Source.Result.Segments);
            }, ex => _logger.LogError(ex, "COMMAND HANDLER ERROR")),
        ];
    }
    private async Task<TranscriptionResult> ProcessChunk(IList<AudioFrame> chunks)
    {
        _logger.LogInformation("STT Processing chunk ({ChunckCount} frames)", chunks.Count);

        var text = await _whisper.TranscribeAsync(chunks.SelectMany(c => c.Buffer).ToArray());

        return new TranscriptionResult(text, DateTime.UtcNow);
    }

    public void Stop()
    {
        _audio.Stop();
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
        _whisper.Load(WhisperModels.ModelSmall, "fr");
        _logger.LogInformation("{TranscribeService} loaded", typeof(ITranscribeService).Name);
    }
}