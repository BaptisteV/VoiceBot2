namespace VoiceBot2.Core.Audio;

public static class AudioUtils
{
    public static bool IsSilence(byte[] buffer, int threshold = 500)
    {
        for (int i = 0; i < buffer.Length; i += 2)
        {
            int sample = BitConverter.ToInt16(buffer, i);
            if (Math.Abs(sample) > threshold)
                return false;
        }
        return true;
    }
}