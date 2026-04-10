using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Whisper.net;

namespace VoiceBot2.Win.Voices;

/// <summary>
/// Listens to the default microphone and emits transcribed French text chunks
/// </summary>
public sealed class VoiceListener : IVoiceListener, IDisposable
{
    private const int SampleRate = 16_000;

    private readonly string _modelPath;

    private readonly ILogger<VoiceListener> _logger;
    private readonly Subject<string> _subject = new();
    private readonly CancellationTokenSource _cts = new();

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private readonly WaveInEvent _waveIn;

    // Ring-buffer of float samples for the current utterance.
    // Pre-allocated; extended only when MaxSegmentDuration is approached.
    private float[] _audioBuffer = new float[SampleRate * 4]; // 12 s ceiling
    private int _audioSamples = 0;

    // Whisper inference runs on a dedicated thread to keep the audio callback
    // non-blocking. Channel is effectively a capacity-1 signal.
    //private readonly SemaphoreSlim _inferenceSignal = new(0, 1);
    private Task? _inferenceTask;

    private const string ModelTiny = @"C:\Users\Bapt\Downloads\ggml-tiny-fp16.bin";
    private const string ModelSmall = @"C:\Users\Bapt\Downloads\ggml-small-fp16.bin";
    private const string ModelMedium = @"C:\Users\Bapt\Downloads\ggml-medium.bin";
    public VoiceListener(
        ILogger<VoiceListener> logger,
        string modelPath = ModelSmall)
    {
        _modelPath = modelPath;
        _logger = logger;

        // NAudio WaveIn: 16 kHz, mono, 16-bit PCM (cheapest conversion).
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            // 100 ms NAudio buffer – low enough not to delay; callback accumulates.
            BufferMilliseconds = 100,
        };
    }

    private void StartListening()
    {
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _inferenceTask = Task.Run(InferenceLoopAsync);
    }

    private void StopListening()
    {
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.StopRecording();
        _audioBuffer = new float[SampleRate * 12];
        _audioSamples = 0;
    }

    private bool _isOn = true;

    /// <summary>
    /// Returns a cold observable. Subscribing starts the microphone.
    /// Disposing the subscription stops it.
    /// Multiple subscriptions share one audio/inference pipeline.
    /// </summary>
    public IObservable<string> VoiceChunks() =>
         Observable.Create<string>(observer =>
         {
             EnsureStarted();
             var subscription = _subject.Subscribe(observer);
             return Disposable.Create(() =>
             {
                 subscription.Dispose();
                 StopListening();
             });
         })
        .Publish()
        .RefCount();

    private void EnsureStarted()
    {
        if (_inferenceTask != null) return; // already running

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



        if (_isOn)
        {
            StartListening();
        }
        else
        {
            StopListening();
        }
    }

    private void ProgressHandler(int progress)
    {
        _logger.LogDebug("Whisper progress {Progress}", progress);
    }

    private void SegHandler(SegmentData e)
    {
        _logger.LogInformation("SegHandler {SegData}", e.Text);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var waveBuffer = new WaveBuffer(e.Buffer);
        int sampleCount = e.BytesRecorded / 2; // 16-bit samples

        // Grow buffer if needed
        if (_audioSamples + sampleCount > _audioBuffer.Length)
        {
            var larger = new float[_audioBuffer.Length * 2];
            _audioBuffer.AsSpan(0, _audioSamples).CopyTo(larger);
            _audioBuffer = larger;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            // Convert short to float in range [-1, 1]
            _audioBuffer[_audioSamples + i] = waveBuffer.ShortBuffer[i] / 32768f;
        }
        _audioSamples += sampleCount;
    }

    private async Task InferenceLoopAsync()
    {
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_isOn)
                {
                    continue;
                }

                var samplesSnapshot = _audioSamples;
                if (samplesSnapshot <= SampleRate)
                    continue;

                var bufferCopy = new float[samplesSnapshot];
                _audioBuffer.AsSpan(0, samplesSnapshot).CopyTo(bufferCopy);

                _audioSamples = 0;

                await TranscribeAsync(bufferCopy, samplesSnapshot, token)
                    .ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in inference loop");
                StopListening();
                StartListening();
            }
        }
    }

    private async Task TranscribeAsync(float[] samples, int sampleCount, CancellationToken token)
    {
        if (_processor == null) return;

        // Wrap the raw float array in a stream Whisper.net can consume.
        // Whisper.net expects a 16-bit WAV stream OR we can use the
        // ProcessAsync(float[]) overload available since v1.5.
        // We use ProcessAsync(Memory<float>) – available in Whisper.net ≥ 1.7.
        var memory = samples.AsMemory(0, sampleCount);

        await foreach (var seg in _processor.ProcessAsync(memory, token))
        {
            var text = seg.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                _subject.OnNext(text);
                _logger.LogDebug("Transcribed: {Transcription}", text);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();

        _waveIn?.StopRecording();
        _waveIn?.Dispose();

        _processor?.Dispose();
        _factory?.Dispose();
        _subject.OnCompleted();
        _subject.Dispose();
        _cts.Dispose();
    }
}