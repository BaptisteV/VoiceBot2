using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;

namespace VoiceBot2.Core.Commands;

public class CommandHandler(ILogger<CommandHandler> logger) : ICommandHandler
{
    private readonly ILogger<CommandHandler> _logger = logger;

    public IObservable<CommandRunner> Handlers(IObservable<CommandResult> commands)
    {
        return commands.SelectMany(DoStuff);
    }

    private IEnumerable<CommandRunner> DoStuff(CommandResult cmd)
    {
        var task = cmd.Type switch
        {
            CommandType.WriteText => HandleWriteTextCommand(cmd),
            _ => null
        };

        if (task is null)
        {
            _logger.LogWarning("Unable to handle command of type {CommandType}: {CommandText}", cmd.Type, cmd.Source.Result.Segments);
            task = Task.CompletedTask;
        }

        yield return new CommandRunner(task, cmd);
    }

    private async Task HandleWriteTextCommand(CommandResult cmd)
    {
        var textToWrite = cmd.Args.TrimWithPunctuation();
        _logger.LogWarning("Simulating text input: {Text}", textToWrite);
    }
}