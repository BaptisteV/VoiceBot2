namespace VoiceBot2.Core.Abstractions;

public interface ISwitch : IDisposable
{
    void Set(bool state);
    IObservable<bool> State();
}
