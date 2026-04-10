namespace VoiceBot2.Win.Voices;

public interface IMicRecorder
{
    IObservable<float[]> VoiceRecord();
}
