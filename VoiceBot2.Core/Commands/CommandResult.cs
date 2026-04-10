using System.Globalization;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Core.Commands;

public enum CommandType
{
    None = 0,
    // Ecrit
    WriteText,
}

public record CommandResult(
    CommandType Type,
    string Command,
    string Args,
    DateTime DetectedAt,
    TimedResult Source
);
public partial class BufferedCommandDetector : ICommandDetector
{
    [GeneratedRegex(@"\bOK[\p{P}\s]+(.*?)\s*Stop\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "fr-FR")]
    private static partial Regex CommandRegex { get; }

    [GeneratedRegex(@"^\b[eé]cri[ts]?\b[\p{P}\s]*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WriteCommandRegex { get; }


    public IObservable<CommandResult> DetectCommands(IObservable<TimedResult> source)
    {
        return source
            .Scan(new BufferState(), (state, input) => state.Append(input))
            .SelectMany(state => state.ExtractCommands());
    }

    private sealed class BufferState
    {
        private readonly List<TimedResult> _segments = [];
        private string _buffer = "";

        public BufferState Append(TimedResult input)
        {
            var text = input.Result.Text?.TrimWithPunctuation();
            if (string.IsNullOrWhiteSpace(text))
                return this;

            _segments.Add(input);
            _buffer += " " + text;
            _buffer = _buffer.Trim();

            return this;
        }

        public IEnumerable<CommandResult> ExtractCommands()
        {
            var matches = CommandRegex.Matches(_buffer);

            if (matches.Count == 0)
                yield break;

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                var inner = match.Groups[1].Value.Trim();

                var normalized = inner.RemoveDiacritics();

                var writeMatch = WriteCommandRegex.Match(normalized);

                CommandType type = CommandType.None;
                string command = "";
                string args = "";

                if (writeMatch.Success)
                {
                    type = CommandType.WriteText;
                    command = "write";
                    args = writeMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // fallback (optional)
                    var parts = inner.Split(' ', 2);
                    command = parts[0];
                    args = parts.Length > 1 ? parts[1] : "";
                }

                yield return new CommandResult(
                    type,
                    command,
                    args,
                    DateTime.UtcNow,
                    _segments.LastOrDefault()!
                );

                lastIndex = match.Index + match.Length;
            }

            // Remove processed text from buffer
            _buffer = _buffer[lastIndex..].Trim();

            // Optional: clean old segments too (simple approach = clear)
            _segments.Clear();
        }
    }
}

public static class StringExtensions
{
    extension(string input)
    {
        public string TrimWithPunctuation()
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            int start = 0;
            int end = input.Length - 1;

            while (start <= end && char.IsPunctuation(input[start]))
                start++;
            while (end >= start && char.IsPunctuation(input[end]))
                end--;

            if (start > end)
                return string.Empty;

            return input.Substring(start, end - start + 1).Trim();
        }

        public string RemoveDiacritics()
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
