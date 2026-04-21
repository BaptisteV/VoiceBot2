using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.Commands;
using VoiceBot2.Core.Model;
using VoiceBot2.Core.SpeechToText;

namespace VoiceBot2.Core;

public record TimedChunk(byte[] Pcm, AudioSegment Segment, DateTime EnqueuedAt);

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

        var textStream = BuildTextStream();
        var commandStream = _commandDetector.DetectCommands(textStream);
        var handlerStream = _commandHandler.Handlers(commandStream);

        _subscriptions =
        [
            SubscribeTextLogging(textStream),
            SubscribeCommandLogging(commandStream),
            SubscribeCommandHandling(handlerStream),
        ];
    }
    private IObservable<TimedResult> BuildTextStream()
    {
        var audioStream = _audio.AudioStream.Publish().RefCount();
        var segments = BuildSegments(audioStream);

        return segments
    .StitchBoundaries(() => _carryOver)
            .Select(pair => new TimedChunk(pair.Pcm, pair.Segment, DateTime.UtcNow))
            .ObserveOn(TaskPoolScheduler.Default)
            .Select(timedChunk => Observable.FromAsync(() => TranscribeChunk(timedChunk)))
            .Merge(2)
            .Publish()
            .RefCount();
    }
    private IObservable<AudioSegment> BuildSegments(IObservable<AudioFrame> audioStream)
    {
        return _audioSegmenter
            .Segment(audioStream,
                silenceDuration: TimeSpan.FromMilliseconds(400),
                maxDuration: TimeSpan.FromSeconds(5))
            .Scan(
                seed: (Buffer: new List<AudioFrame>(), Ready: (AudioSegment?)null),
                accumulator: (state, segment) =>
                {
                    var merged = state.Buffer.Concat(segment.Frames).ToList();
                    return merged.Count >= 50
                        ? (Buffer: new List<AudioFrame>(), Ready: new AudioSegment(merged, segment.Reason))
                        : (Buffer: merged, Ready: null);
                })
            .Where(state => state.Ready is not null)
            .Select(state => state.Ready!);
    }

    private byte[] _carryOver = [];

    private async Task<TimedResult> TranscribeChunk(TimedChunk timedChunk)
    {
        var startProcessing = DateTime.UtcNow;
        var queueDelay = startProcessing - timedChunk.EnqueuedAt;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("STT Processing chunk ({FrameCount} frames)", timedChunk.Segment.Frames.Count);
        var transcription = await _whisper.TranscribeAsync(timedChunk.Pcm);

        // Decide whether to carry tail bytes into the next chunk
        _carryOver = BoundaryStitcher.ExtractCarryOver(timedChunk.Pcm, transcription, timedChunk.Segment.Reason);
        if (_carryOver.Length > 0)
            _logger.LogDebug("Carrying over {Bytes} bytes to next chunk to avoid mid-word cut", _carryOver.Length);

        var result = new TranscriptionResult(transcription, DateTime.UtcNow);
        var r = new TimedResult(result, queueDelay, sw.Elapsed, DateTime.UtcNow - timedChunk.EnqueuedAt);
        _timedResultSubject.OnNext(r);
        return r;
    }

    private async Task<TranscriptionResult> ProcessChunk(IList<AudioFrame> chunks)
    {
        _logger.LogInformation("STT Processing chunk ({ChunkCount} frames)", chunks.Count);
        var buffer = chunks.SelectMany(c => c.Buffer).ToArray();
        var text = await _whisper.TranscribeAsync(buffer);
        return new TranscriptionResult(text, DateTime.UtcNow);
    }

    private IDisposable SubscribeTextLogging(IObservable<TimedResult> textStream) =>
        textStream.Subscribe(
            x => _logger.LogInformation(
                "STT={ProcessingTime}ms | TEXT={Text} | QUEUE={QueueDelay}ms | E2E={EndToEndLatency}ms",
                x.ProcessingTime.TotalMilliseconds.ToString("F0"),
                x.Result.Segments.ConcatenatedText,
                x.QueueDelay.TotalMilliseconds.ToString("F0"),
                x.EndToEndLatency.TotalMilliseconds.ToString("F0")),
            ex => _logger.LogError(ex, "TEXT STREAM ERROR"));

    private IDisposable SubscribeCommandLogging(IObservable<CommandResult> commandStream) =>
        commandStream.Subscribe(
            cmd => _logger.LogInformation("COMMAND DETECTED: {CommandText}", cmd.Source.Result.Segments),
            ex => _logger.LogError(ex, "COMMAND STREAM ERROR"));

    private IDisposable SubscribeCommandHandling(IObservable<CommandRunner> handlerStream) =>
        handlerStream.Subscribe(
            async h =>
            {
                await h.Run;
                _logger.LogInformation("COMMAND HANDLED: {CommandText}", h.Result.Source.Result.Segments);
            },
            ex => _logger.LogError(ex, "COMMAND HANDLER ERROR"));

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
        _whisper.Load(WhisperModels.ModelLargeTurbo, "fr");
        _logger.LogInformation("{TranscribeService} loaded", typeof(ITranscribeService).Name);
    }
}