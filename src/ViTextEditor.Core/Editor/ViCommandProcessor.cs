using System.Text.RegularExpressions;

namespace ViTextEditor.Core.Editor;

public sealed class ViCommandProcessor
{
    private static readonly Regex CommandPattern = new(
        @"^(?<range>%|(?:[.$]|\d+)(?:\s*,\s*(?:[.$]|\d+))?)?\s*(?<command>y(?:a(?:n(?:k)?)?)?|pu(?:t)?)?\s*(?<register>[A-Za-z])?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IEditorAdapter _editor;
    private readonly ViRegisterStore _registers;

    public ViCommandProcessor(IEditorAdapter editor, ViRegisterStore? registers = null)
    {
        _editor = editor;
        _registers = registers ?? new ViRegisterStore();
    }

    public bool Execute(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return false;
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
        var match = CommandPattern.Match(command.Trim());
        return match.Success && IsPutCommand(match.Groups["command"].Value);
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
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
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
