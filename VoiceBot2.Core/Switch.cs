using System.Reactive.Linq;
using System.Reactive.Subjects;
using VoiceBot2.Core.Abstractions;

namespace VoiceBot2.Core;

public sealed class Switch : ISwitch
{
    private readonly Subject<bool> _subject = new();

    public void Set(bool state)
    {
        _subject.OnNext(state);
    }

    public IObservable<bool> State()
    {
        return Observable.Create<bool>(observer =>
        {
            var subscription = _subject.Subscribe(observer);
            return subscription;
        });
    }

    public void Dispose()
    {
        _subject.Dispose();
    }
}