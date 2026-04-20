using Microsoft.Extensions.Logging;
using NAudio.Utils;
using NAudio.Wave;
using VoiceBot2.Core.Abstractions;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace VoiceBot2.Core.SpeechToText;

public sealed class WhisperService(ILogger<WhisperService> logger) : ITranscribeService
{
    private WhisperProcessor? _processor;
    private readonly ILogger<WhisperService> _logger = logger;

    public async Task<string> TranscribeAsync(byte[] pcmData)
    {
        await using var ms = new MemoryStream();
        await using var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), new WaveFormat(16000, 16, 1));

        await writer.WriteAsync(pcmData);
        await writer.FlushAsync();
        ms.Seek(0, SeekOrigin.Begin);
        var segments = await _processor!.ProcessAsync(ms).ToListAsync();
        var speechSegments = segments.Where(s => s.NoSpeechProbability > 2.0E-06).Select(r => r.Text);
        return string.Concat(speechSegments);
    }

    public void Load(string modelPath, string language)
    {
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cuda, RuntimeLibrary.Cpu];

        var factory = WhisperFactory.FromPath(modelPath);

        _logger.LogInformation("Loaded the {Backend} backend", RuntimeOptions.LoadedLibrary);

        _processor = factory.CreateBuilder()
            //.WithPrintProgress()
            //.WithPrintResults()
            .WithPrompt("Transcription fidèle de la parole humaine uniquement. Ignore les bruits de fond, la musique, les respirations et tout son non verbal. Ne transcris que les mots clairement prononcés par une personne.")
            .WithCarryInitialPrompt(true)
            //.SplitOnWord()
            //.WithNoSpeechThreshold(1.0f)
            .WithLanguage(language)
            .WithNoContext()
            //.WithSingleSegment()
            //.WithProgressHandler(ProgressHandler)
            //.WithTemperature(0.2f)
            //.WithSegmentEventHandler(SegHandler)
            .Build();
    }

    private void SegHandler(SegmentData e)
    {
        //throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        await _processor!.DisposeAsync();
    }
}