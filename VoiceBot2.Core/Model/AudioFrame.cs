
namespace VoiceBot2.Core.Model;

public record AudioFrame(byte[] Buffer, int SampleRate, DateTime Timestamp)
{
    public bool IsSilent()
    {
        return IsSilence(Buffer);
    }

    private static bool IsSilence(byte[] buffer, double threshold = 0.075)
    {
        var (_, rms) = ComputeVolume(buffer);
        if (rms < threshold)
            return true;
        return false;
    }

    private static (double Volume, double RmsVolume) ComputeVolume(byte[] pcmData)
    {
        int sampleCount = pcmData.Length / 2; // 16-bit = 2 bytes per sample

        double maxAmplitude = 0;
        double sumOfSquares = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            // Convert 2 bytes to a signed 16-bit sample (little-endian)
            short sample = BitConverter.ToInt16(pcmData, i * 2);

            // Normalize to [-1.0, 1.0]
            double normalized = sample / 32768.0;

            // Peak volume
            maxAmplitude = Math.Max(maxAmplitude, Math.Abs(normalized));

            // RMS accumulation
            sumOfSquares += normalized * normalized;
        }

        double rms = Math.Sqrt(sumOfSquares / sampleCount);

        return (maxAmplitude, rms);
    }
}
