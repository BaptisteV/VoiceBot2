using VoiceBot2.Core.Commands;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Abstractions;

public interface ICommandDetector
{
    IObservable<CommandResult> DetectCommands(IObservable<TimedResult> source);
}
