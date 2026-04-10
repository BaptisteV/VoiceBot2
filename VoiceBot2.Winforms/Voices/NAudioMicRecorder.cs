using NAudio.Wave;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace VoiceBot2.Win.Voices;

public class NAudioMicRecorder : IMicRecorder
{
    private const int TranscribeEvery = 48_000;
    private readonly ConcurrentQueue<float> _bufferQueue = new();

    private readonly WaveInEvent _waveIn;

    public NAudioMicRecorder()
    {
        // NAudio WaveIn: 16 kHz, mono, 16-bit PCM (cheapest conversion).
        _waveIn = new WaveInEvent
        {
            // 100 ms NAudio buffer – low enough not to delay; callback accumulates.
            BufferMilliseconds = 100,
        };
    }

    private static float[] ConvertToFloat(byte[] buffer, int byteCount, NAudio.Wave.WaveFormat format)
    {
        // 32-bit IEEE float – no conversion needed, just reinterpret.
        if (format.Encoding.HasFlag(WaveFormatEncoding.IeeeFloat) && format.BitsPerSample == 32)
        {
            int sampleCount = byteCount / 4;
            float[] samples = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, samples, 0, byteCount);
            return samples;
        }

        // 32-bit PCM (signed int) – normalize to [-1, 1]
        if (format.Encoding.HasFlag(WaveFormatEncoding.Extensible) && format.BitsPerSample == 32)
        {
            int sampleCount = byteCount / 4;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt32(buffer, i * 4) / 2147483648f;
            return samples;
        }

        // 16-bit PCM – divide by 32768 to normalize.
        if (format.Encoding.HasFlag(WaveFormatEncoding.Pcm) && format.BitsPerSample == 16)
        {
            int sampleCount = byteCount / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
            return samples;
        }

        throw new NotSupportedException(
            $"Unsupported format: {format.Encoding}, {format.BitsPerSample}-bit");
    }

    public IObservable<float[]> VoiceRecord() =>
        Observable.Create<float[]>(observer =>
        {
            _waveIn.DataAvailable += (object? sender, WaveInEventArgs e) =>
                {
                    try
                    {
                        float[] floats = ConvertToFloat(e.Buffer, e.BytesRecorded, _waveIn.WaveFormat);
                        foreach (var f in floats)
                        {
                            _bufferQueue.Enqueue(f);
                        }
                        if (_bufferQueue.Count > TranscribeEvery)
                        {
                            observer.OnNext(_bufferQueue.ToArray());
                            _bufferQueue.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                };

            _waveIn.StartRecording();

            // Returned disposable is called when the subscriber disposes/unsubscribes.
            return Disposable.Create(() =>
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
            });
        });

}
