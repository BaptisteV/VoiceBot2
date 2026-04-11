using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Commands;

public record CommandResult(
    CommandType Type,
    string Command,
    string Args,
    DateTime DetectedAt,
    TimedResult Source
);
