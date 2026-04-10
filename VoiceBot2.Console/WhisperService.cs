using NAudio.Utils;
using NAudio.Wave;
using Whisper.net;

public class WhisperService : IDisposable
{
    private readonly WhisperProcessor _processor;

    public WhisperService(string modelPath, string language)
    {
        var factory = WhisperFactory.FromPath(modelPath);

        _processor = factory.CreateBuilder()
            .WithPrompt("Transcribe the audio coming from the user's microphone")
            .WithCarryInitialPrompt(true)
            //.SplitOnWord()
            //.WithNoSpeechThreshold(1.0f)
            .WithLanguage(language)
            //.WithNoContext()
            //.WithSingleSegment()
            //.WithProgressHandler(ProgressHandler)
            //.WithTemperature(0.2f)
            //.WithSegmentEventHandler(SegHandler)
            .Build();
    }

    public async Task<string> TranscribeAsync(byte[] pcmData)
    {
        await using var ms = new MemoryStream();

        await using (var writer = new WaveFileWriter(
            new IgnoreDisposeStream(ms),
            new WaveFormat(16000, 16, 1)))
        {
            await writer.WriteAsync(pcmData);
        }

        ms.Position = 0;

        var result = new List<string>();

        await foreach (var segment in _processor.ProcessAsync(ms))
        {
            result.Add(segment.Text);
        }

        return string.Join(" ", result);
    }

    public void Dispose()
    {
        _processor.Dispose();
    }
}