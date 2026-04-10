namespace VoiceBot2.Win.Voices;

public interface IVoiceListener
{
    IObservable<string> VoiceChunks();
}