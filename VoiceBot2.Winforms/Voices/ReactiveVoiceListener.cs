using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Whisper.net;

namespace VoiceBot2.Win.Voices;

public class ReactiveVoiceListener : IVoiceListener
{
    private const string ModelSmall = @"C:\Users\Bapt\Downloads\ggml-small-fp16.bin";
    private readonly string _modelPath = ModelSmall;
    private readonly Subject<string> _subject = new();
    private readonly IMicRecorder _recorder;
    private readonly ILogger<ReactiveVoiceListener> _logger;
    private readonly WhisperFactory _factory;
    private readonly WhisperProcessor _processor;

    public ReactiveVoiceListener(IMicRecorder recorder, ILogger<ReactiveVoiceListener> logger)
    {
        _recorder = recorder;
        _logger = logger;
        _factory = WhisperFactory.FromPath(_modelPath);
        _processor = _factory.CreateBuilder()
            .SplitOnWord()
            //.WithNoSpeechThreshold(1.0f) // more aggressive VAD (default is 0.6)
            .WithLanguage("fr")          // French only – fastest, most accurate
                                         .WithNoContext()             // don't carry context across segments
                                         .WithSingleSegment()        // one segment per ProcessAsync call
                        .WithProgressHandler(ProgressHandler)
            //.WithTemperature(0.2f)        // greedy; fastest + most deterministic

            .WithSegmentEventHandler(SegHandler)
            .Build();
        _recorder.VoiceRecord().Subscribe(async (b) => await OnMicData(b));
    }

    private void SegHandler(SegmentData e)
    {
        _subject.OnNext(e.Text);
        _logger.LogInformation("Reactive SegHandler {SegData}", e.Text);
    }

    private void ProgressHandler(int progress)
    {
        _logger.LogDebug("Reactive Whisper progress {Progress}", progress);
    }

    private async Task OnMicData(float[] buffer)
    {
        var memory = buffer.AsMemory(0, buffer.Length);

        await foreach (var seg in _processor.ProcessAsync(memory, CancellationToken.None))
        {
            var text = seg.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                //_subject.OnNext(text);
                _logger.LogDebug("Reactive OnMicData: {Transcription}", text);
            }
        }
    }

    public IObservable<string> VoiceChunks() =>
         Observable.Create<string>(observer =>
         {
             var subscription = _subject.Subscribe(observer);
             return Disposable.Create(() =>
             {
                 subscription.Dispose();
                 //StopListening();
             });
         })
        .Publish()
        .RefCount();
}
