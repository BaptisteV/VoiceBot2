using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace VoiceBot2.Win.Voices;

public sealed class CsMicRecorder : IMicRecorder, IDisposable
{
    private WasapiCapture? _capture;
    private readonly ConcurrentQueue<float> _bufferQueue = new();

    private static readonly int TranscribeEvery = 48_000;

    public IObservable<float[]> VoiceRecord() =>
        Observable.Create<float[]>(observer =>
        {
            _capture = new WasapiCapture
            {
                Device = GetDefaultMicrophone(),
            };
            _capture.Stopped += (_, _) => observer.OnCompleted();


            // Initialise with a standard format: 16-bit, 16 kHz, mono.
            // Adjust to taste (e.g. 44100 Hz stereo).
            _capture.Initialize();

            // CSCore fires DataAvailable on a background thread – that's fine
            // for Rx, observers should be thread-safe by contract.
            var waveFormat = _capture.WaveFormat;

            _capture.DataAvailable += (_, e) =>
            {
                try
                {
                    float[] floats = ConvertToFloat(e.Data, e.ByteCount, waveFormat);
                    foreach (var f in floats)
                    {
                        _bufferQueue.Enqueue(f);

                        observer.OnNext(_bufferQueue.ToArray());
                    }
                    if (_bufferQueue.Count > TranscribeEvery)
                    {
                        _bufferQueue.Clear();
                    }
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            };

            _capture.Start();

            // Returned disposable is called when the subscriber disposes/unsubscribes.
            return Disposable.Create(() =>
            {
                _capture.Stop();
                _capture.Dispose();
            });
        });

    private static MMDevice GetDefaultMicrophone() =>
        MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

    private static float[] ConvertToFloat(byte[] buffer, int byteCount, WaveFormat format)
    {
        // 32-bit IEEE float – no conversion needed, just reinterpret.
        if (format.WaveFormatTag == AudioEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            int sampleCount = byteCount / 4;
            float[] samples = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, samples, 0, byteCount);
            return samples;
        }

        // 32-bit PCM (signed int) – normalize to [-1, 1]
        if (format.WaveFormatTag == AudioEncoding.Extensible && format.BitsPerSample == 32)
        {
            int sampleCount = byteCount / 4;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt32(buffer, i * 4) / 2147483648f;
            return samples;
        }

        // 16-bit PCM – divide by 32768 to normalize.
        if (format.WaveFormatTag == AudioEncoding.Pcm && format.BitsPerSample == 16)
        {
            int sampleCount = byteCount / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
            return samples;
        }

        throw new NotSupportedException(
            $"Unsupported format: {format.WaveFormatTag}, {format.BitsPerSample}-bit");
    }

    public void Dispose()
    {
        _capture?.Stop();
        _capture?.Dispose();
    }
}
