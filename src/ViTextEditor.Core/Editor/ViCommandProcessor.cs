using System.Text;
using System.Text.RegularExpressions;

namespace ViTextEditor.Core.Editor;

public sealed class ViCommandProcessor
{
    private static readonly Regex CommandPattern = new(
        @"^(?<range>%|(?:[.$]|\d+)(?:\s*,\s*(?:[.$]|\d+))?)?\s*(?<command>y(?:a(?:n(?:k)?)?)?|pu(?:t)?)?\s*(?<register>[A-Za-z])?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CompactYankCountPattern = new(
        @"^y(?<count>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex YankCountPattern = new(
        @"^(?<command>y(?:a(?:n(?:k)?)?)?)\s+(?:(?<register>[A-Za-z])\s+)?(?<count>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DeletePattern = new(
        @"^(?<range>%|(?:[.$]|\d+)(?:\s*,\s*(?:[.$]|\d+))?)?\s*(?<command>d|delete)(?:\s+(?<register>[A-Za-z]))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SubstitutePrefixPattern = new(
        @"^(?<range>%|(?:[.$]|\d+)(?:\s*,\s*(?:[.$]|\d+))?)?\s*(?<command>s|substitute)(?![A-Za-z])(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IEditorAdapter _editor;
    private readonly ViRegisterStore _registers;
    private readonly ViOptions _options;
    private string? _lastSubstitutePattern;
    private string? _lastSubstituteReplacement;

    public ViCommandProcessor(IEditorAdapter editor, ViRegisterStore? registers = null, ViOptions? options = null)
    {
        _editor = editor;
        _registers = registers ?? new ViRegisterStore();
        _options = options ?? ViOptions.Shared;
    }

    public string? LastMessage { get; private set; }

    public bool Execute(string command)
    {
        LastMessage = null;
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (_options.TryExecuteSet(trimmed, out var optionMessage))
        {
            LastMessage = optionMessage;
            return true;
        }

        if (trimmed == "&")
        {
            return RepeatLastSubstitute();
        }

        if (TryExecuteSubstitute(trimmed, out var substituteResult))
        {
            return substituteResult;
        }

        if (TryExecuteDelete(trimmed, out var deleteResult))
        {
            return deleteResult;
        }

        if (TryExecuteYankCount(trimmed, out var yankCountResult))
        {
            return yankCountResult;
        }

        var match = CommandPattern.Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var rangeText = match.Groups["range"].Value;
        var commandText = match.Groups["command"].Value;
        var registerText = match.Groups["register"].Value;
        var register = registerText.Length == 1 ? registerText[0] : (char?)null;

        if (commandText.Length == 0)
        {
            if (rangeText.Length == 0 || register is not null)
            {
                return false;
            }

            if (!rangeText.Contains(',') && int.TryParse(rangeText, out var requestedLine) && requestedLine >= 1)
            {
                MoveToLine(Math.Min(requestedLine, CountLines(_editor.Text)));
                return true;
            }

            if (!TryResolveRange(rangeText, allowZero: false, out _, out var endLine))
            {
                return false;
            }

            MoveToLine(endLine);
            return true;
        }

        if (IsYankCommand(commandText))
        {
            if (!TryResolveRangeOrCurrent(rangeText, allowZero: false, out var startLine, out var endLine))
            {
                return false;
            }

            return YankLines(startLine, endLine, register);
        }

        if (IsPutCommand(commandText))
        {
            if (!TryResolvePutLine(rangeText, out var lineNumber))
            {
                return false;
            }

            return PutAfterLine(lineNumber, register);
        }

        return false;
    }

    public bool IsMutatingCommand(string command)
    {
        var trimmed = command.Trim();
        if (IsOptionCommand(trimmed)) return false;
        if (SubstitutePrefixPattern.IsMatch(trimmed) || DeletePattern.IsMatch(trimmed) || trimmed == "&") return true;
        var match = CommandPattern.Match(trimmed);
        return match.Success && IsPutCommand(match.Groups["command"].Value);
    }

    public bool IsOptionCommand(string command)
    {
        var trimmed = command.TrimStart();
        return trimmed.Equals("set", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("se", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("set ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("se ", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryExecuteSubstitute(string command, out bool result)
    {
        result = false;
        var match = SubstitutePrefixPattern.Match(command);
        if (!match.Success) return false;

        var rangeText = match.Groups["range"].Value;
        var rest = match.Groups["rest"].Value;
        if (rest.Length == 0)
        {
            result = RepeatLastSubstitute(rangeText);
            return true;
        }

        var delimiter = rest[0];
        if (char.IsLetterOrDigit(delimiter) || char.IsWhiteSpace(delimiter))
        {
            LastMessage = "E146: Regular expressions can't be delimited by letters";
            result = true;
            return true;
        }

        var cursor = 1;
        if (!TryReadDelimited(rest, ref cursor, delimiter, out var pattern) ||
            !TryReadDelimited(rest, ref cursor, delimiter, out var replacement))
        {
            LastMessage = "E488: Trailing characters";
            result = true;
            return true;
        }

        var flags = rest[cursor..].Trim();
        if (flags.Any(ch => ch is not ('g' or 'i' or 'I' or 'e')))
        {
            LastMessage = $"E488: Trailing characters: {flags}";
            result = true;
            return true;
        }

        if (string.IsNullOrEmpty(pattern))
        {
            pattern = _lastSubstitutePattern ?? string.Empty;
        }
        if (string.IsNullOrEmpty(pattern))
        {
            LastMessage = "E33: No previous substitute regular expression";
            result = true;
            return true;
        }

        _lastSubstitutePattern = pattern;
        _lastSubstituteReplacement = replacement;
        result = ExecuteSubstitute(rangeText, pattern, replacement, flags);
        return true;
    }

    private bool RepeatLastSubstitute(string rangeText = "")
    {
        if (string.IsNullOrEmpty(_lastSubstitutePattern) || _lastSubstituteReplacement is null)
        {
            LastMessage = "E33: No previous substitute regular expression";
            return true;
        }
        return ExecuteSubstitute(rangeText, _lastSubstitutePattern, _lastSubstituteReplacement, string.Empty);
    }

    private bool ExecuteSubstitute(string rangeText, string pattern, string replacement, string flags)
    {
        if (!TryResolveRangeOrCurrent(rangeText, allowZero: false, out var startLine, out var endLine)) return false;

        var ignoreCase = flags.Contains('i') ? true : flags.Contains('I') ? false : _options.IgnoreCase;
        var regexOptions = RegexOptions.CultureInvariant | RegexOptions.Multiline;
        if (ignoreCase) regexOptions |= RegexOptions.IgnoreCase;

        Regex regex;
        try
        {
            regex = new Regex(pattern, regexOptions);
        }
        catch (ArgumentException ex)
        {
            LastMessage = $"Invalid pattern: {ex.Message}";
            return true;
        }

        var translatedReplacement = TranslateReplacement(replacement);
        var originalText = _editor.Text;
        var starts = BuildLineStarts(originalText);
        var changed = false;
        var lastChangedPosition = -1;

        for (var line = endLine; line >= startLine; line--)
        {
            var start = starts[line - 1];
            var end = line < starts.Count ? TrimLineTerminator(originalText, starts[line]) : originalText.Length;
            var originalLine = originalText[start..end];
            var replaced = flags.Contains('g')
                ? regex.Replace(originalLine, translatedReplacement)
                : regex.Replace(originalLine, translatedReplacement, 1);
            if (string.Equals(originalLine, replaced, StringComparison.Ordinal)) continue;

            _editor.DeleteRange(start, end - start);
            _editor.InsertText(start, replaced);
            changed = true;
            lastChangedPosition = start;
        }

        if (changed)
        {
            _editor.MoveCaret(Math.Max(0, lastChangedPosition));
            return true;
        }

        if (!flags.Contains('e')) LastMessage = $"E486: Pattern not found: {pattern}";
        return true;
    }

    private static bool TryReadDelimited(string text, ref int cursor, char delimiter, out string value)
    {
        var builder = new StringBuilder();
        var escaped = false;
        while (cursor < text.Length)
        {
            var ch = text[cursor++];
            if (escaped)
            {
                if (ch == delimiter) builder.Append(delimiter);
                else
                {
                    builder.Append('\\');
                    builder.Append(ch);
                }
                escaped = false;
                continue;
            }
            if (ch == '\\')
            {
                escaped = true;
                continue;
            }
            if (ch == delimiter)
            {
                value = builder.ToString();
                return true;
            }
            builder.Append(ch);
        }

        if (escaped) builder.Append('\\');
        value = builder.ToString();
        return false;
    }

    private static string TranslateReplacement(string replacement)
    {
        var builder = new StringBuilder(replacement.Length + 8);
        for (var i = 0; i < replacement.Length; i++)
        {
            var ch = replacement[i];
            if (ch == '\\' && i + 1 < replacement.Length)
            {
                var next = replacement[++i];
                if (char.IsDigit(next)) builder.Append('$').Append(next);
                else if (next == '&') builder.Append('&');
                else if (next == 'r') builder.Append("\r\n");
                else builder.Append(next);
                continue;
            }
            if (ch == '&') builder.Append("$&");
            else if (ch == '$') builder.Append("$$");
            else builder.Append(ch);
        }
        return builder.ToString();
    }

    private bool TryExecuteDelete(string command, out bool result)
    {
        result = false;
        var match = DeletePattern.Match(command);
        if (!match.Success) return false;

        var rangeText = match.Groups["range"].Value;
        var registerText = match.Groups["register"].Value;
        var register = registerText.Length == 1 ? registerText[0] : (char?)null;
        if (!TryResolveRangeOrCurrent(rangeText, allowZero: false, out var startLine, out var endLine))
        {
            result = false;
            return true;
        }

        result = DeleteLines(startLine, endLine, register);
        return true;
    }

    private bool DeleteLines(int startLine, int endLine, char? register)
    {
        var text = _editor.Text;
        var starts = BuildLineStarts(text);
        if (startLine < 1 || endLine < startLine || endLine > starts.Count) return false;

        var lines = new List<string>(endLine - startLine + 1);
        for (var line = startLine; line <= endLine; line++)
        {
            var contentStart = starts[line - 1];
            var contentEnd = line < starts.Count ? TrimLineTerminator(text, starts[line]) : text.Length;
            lines.Add(text[contentStart..contentEnd]);
        }
        _registers.Yank(register, lines);

        var deleteStart = starts[startLine - 1];
        var deleteEnd = endLine < starts.Count ? starts[endLine] : text.Length;
        _editor.DeleteRange(deleteStart, deleteEnd - deleteStart);
        _editor.MoveCaret(Math.Min(deleteStart, _editor.TextLength));
        return true;
    }

    private bool TryExecuteYankCount(string command, out bool result)
    {
        result = false;
        Match match;
        char? register = null;

        var compact = CompactYankCountPattern.Match(command);
        if (compact.Success)
        {
            match = compact;
        }
        else
        {
            match = YankCountPattern.Match(command);
            if (!match.Success)
            {
                return false;
            }

            var registerText = match.Groups["register"].Value;
            register = registerText.Length == 1 ? registerText[0] : (char?)null;
        }

        if (!int.TryParse(match.Groups["count"].Value, out var count) || count < 1)
        {
            result = false;
            return true;
        }

        var startLine = CurrentLineNumber();
        var lineCount = CountLines(_editor.Text);
        var endLine = Math.Min(lineCount, startLine + count - 1);
        result = YankLines(startLine, endLine, register);
        return true;
    }

    private bool YankLines(int startLine, int endLine, char? register)
    {
        var text = _editor.Text;
        var starts = BuildLineStarts(text);
        if (startLine < 1 || endLine < startLine || endLine > starts.Count)
        {
            return false;
        }

        var lines = new List<string>(endLine - startLine + 1);
        for (var line = startLine; line <= endLine; line++)
        {
            var start = starts[line - 1];
            var end = line < starts.Count ? TrimLineTerminator(text, starts[line]) : text.Length;
            lines.Add(text[start..end]);
        }

        _registers.Yank(register, lines);
        return true;
    }

    private bool PutAfterLine(int lineNumber, char? register)
    {
        if (!_registers.TryGet(register, out var lines) || lines.Count == 0)
        {
            return false;
        }

        var text = _editor.Text;
        var starts = BuildLineStarts(text);
        if (lineNumber < 0 || lineNumber > starts.Count)
        {
            return false;
        }

        var newline = DetectNewLine(text);
        var payload = string.Join(newline, lines);
        int insertAt;
        string insertion;
        int caretTarget;

        if (text.Length == 0)
        {
            insertAt = 0;
            insertion = payload;
            caretTarget = 0;
        }
        else if (lineNumber == 0)
        {
            insertAt = 0;
            insertion = payload + newline;
            caretTarget = 0;
        }
        else if (lineNumber < starts.Count)
        {
            insertAt = starts[lineNumber];
            insertion = payload + newline;
            caretTarget = insertAt;
        }
        else
        {
            insertAt = text.Length;
            insertion = newline + payload;
            caretTarget = insertAt + newline.Length;
        }

        _editor.InsertText(insertAt, insertion);
        _editor.MoveCaret(caretTarget);
        return true;
    }

    private bool TryResolvePutLine(string rangeText, out int lineNumber)
    {
        if (rangeText.Length == 0)
        {
            lineNumber = CurrentLineNumber();
            return true;
        }

        if (rangeText == "%" || rangeText.Contains(','))
        {
            lineNumber = 0;
            return false;
        }

        return TryResolveAddress(rangeText, allowZero: true, out lineNumber);
    }

    private bool TryResolveRangeOrCurrent(string rangeText, bool allowZero, out int startLine, out int endLine)
    {
        if (rangeText.Length == 0)
        {
            startLine = CurrentLineNumber();
            endLine = startLine;
            return true;
        }

        return TryResolveRange(rangeText, allowZero, out startLine, out endLine);
    }

    private bool TryResolveRange(string rangeText, bool allowZero, out int startLine, out int endLine)
    {
        var lineCount = CountLines(_editor.Text);
        if (rangeText == "%")
        {
            startLine = 1;
            endLine = lineCount;
            return true;
        }

        var parts = rangeText.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            if (!TryResolveAddress(parts[0], allowZero, out startLine))
            {
                endLine = 0;
                return false;
            }

            endLine = startLine;
            return true;
        }

        if (parts.Length != 2 ||
            !TryResolveAddress(parts[0], allowZero, out startLine) ||
            !TryResolveAddress(parts[1], allowZero, out endLine))
        {
            startLine = 0;
            endLine = 0;
            return false;
        }

        return startLine <= endLine;
    }

    private bool TryResolveAddress(string address, bool allowZero, out int lineNumber)
    {
        var lineCount = CountLines(_editor.Text);
        if (address == ".")
        {
            lineNumber = CurrentLineNumber();
            return true;
        }

        if (address == "$")
        {
            lineNumber = lineCount;
            return true;
        }

        if (!int.TryParse(address, out lineNumber))
        {
            return false;
        }

        var minimum = allowZero ? 0 : 1;
        return lineNumber >= minimum && lineNumber <= lineCount;
    }

    private int CurrentLineNumber()
    {
        var text = _editor.Text;
        var starts = BuildLineStarts(text);
        var caret = Math.Clamp(_editor.CaretPosition, 0, text.Length);
        var line = starts.BinarySearch(caret);
        if (line >= 0)
        {
            return line + 1;
        }

        return ~line;
    }

    private void MoveToLine(int oneBasedLineNumber)
    {
        var text = _editor.Text;
        var starts = BuildLineStarts(text);
        if (oneBasedLineNumber < 1 || oneBasedLineNumber > starts.Count)
        {
            return;
        }

        _editor.MoveCaret(starts[oneBasedLineNumber - 1]);
    }

    private static List<int> BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                starts.Add(i + 1);
            }
            else if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }
        return starts;
    }

    private static int TrimLineTerminator(string text, int nextLineStart)
    {
        var end = nextLineStart;
        if (end > 0 && text[end - 1] == '\n') end--;
        if (end > 0 && text[end - 1] == '\r') end--;
        return end;
    }

    private static int CountLines(string text) => BuildLineStarts(text).Count;

    private static string DetectNewLine(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (text.Contains('\n')) return "\n";
        if (text.Contains('\r')) return "\r";
        return Environment.NewLine;
    }

    private static bool IsYankCommand(string command) =>
        command.Equals("y", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("ya", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("yan", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("yank", StringComparison.OrdinalIgnoreCase);

    private static bool IsPutCommand(string command) =>
        command.Equals("pu", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("put", StringComparison.OrdinalIgnoreCase);
}
