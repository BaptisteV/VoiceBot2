using VoiceBot2.Core.Commands;

namespace VoiceBot2.Core.Abstractions;

public interface ICommandHandler
{
    IObservable<CommandRunner> Handlers(IObservable<CommandResult> commands);
}
