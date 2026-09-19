namespace ViTextEditor.Core.Editor;

public sealed class ViCommandProcessor
{
    private readonly IEditorAdapter _editor;

    public ViCommandProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public bool Execute(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed == "$")
        {
            MoveToLine(CountLines(_editor.Text));
            return true;
        }

        if (!int.TryParse(trimmed, out var lineNumber) || lineNumber < 1)
        {
            return false;
        }

        MoveToLine(lineNumber);
        return true;
    }

    private void MoveToLine(int oneBasedLineNumber)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        var targetLine = Math.Clamp(oneBasedLineNumber, 1, CountLines(text));
        var position = 0;
        for (var line = 1; line < targetLine; line++)
        {
            var nextBreak = text.IndexOf('\n', position);
            if (nextBreak < 0)
            {
                break;
            }
            position = nextBreak + 1;
        }

        _editor.MoveCaret(position);
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        return text.Count(c => c == '\n') + 1;
    }
}
