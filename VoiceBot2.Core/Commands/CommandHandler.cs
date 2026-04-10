using Microsoft.Extensions.Logging;
using System.Reactive.Linq;

namespace VoiceBot2.Core.Commands;

public record CommandRunner(Task Run, CommandResult Result);

public interface ICommandHandler
{
    IObservable<CommandRunner> Handlers(IObservable<CommandResult> commands);
}

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
            _logger.LogWarning("Unable to handle command of type {CommandType}: {CommandText}", cmd.Type, cmd.Source.Result.Text);
            task = Task.CompletedTask;
        }

        yield return new CommandRunner(task, cmd);
    }

    private async Task HandleWriteTextCommand(CommandResult cmd)
    {
        _logger.LogWarning("Simulating text input: {Text}", cmd.Args);
    }
}
