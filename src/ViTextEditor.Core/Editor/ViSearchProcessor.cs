namespace ViTextEditor.Core.Editor;

public sealed class ViSearchProcessor
{
    private readonly IEditorAdapter _editor;
    private string? _lastPattern;
    private int _lastDirection = 1;

    public ViSearchProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public string? LastPattern => _lastPattern;

    public bool Search(string pattern, bool forward)
    {
        if (string.IsNullOrEmpty(pattern) || _editor.Text.Length == 0)
        {
            return false;
        }

        _lastPattern = pattern;
        _lastDirection = forward ? 1 : -1;
        return Find(pattern, _lastDirection);
    }

    public bool Repeat(bool reverseDirection)
    {
        if (string.IsNullOrEmpty(_lastPattern))
        {
            return false;
        }

        var direction = reverseDirection ? -_lastDirection : _lastDirection;
        return Find(_lastPattern, direction);
    }

    private bool Find(string pattern, int direction)
    {
        var text = _editor.Text;
        if (text.Length == 0 || pattern.Length == 0)
        {
            return false;
        }

        int match;
        if (direction > 0)
        {
            var start = Math.Min(_editor.CaretPosition + 1, text.Length);
            match = start < text.Length
                ? text.IndexOf(pattern, start, StringComparison.Ordinal)
                : -1;
            if (match < 0)
            {
                match = text.IndexOf(pattern, 0, StringComparison.Ordinal);
            }
        }
        else
        {
            var start = _editor.CaretPosition <= 0
                ? text.Length - 1
                : Math.Min(_editor.CaretPosition - 1, text.Length - 1);
            match = text.LastIndexOf(pattern, start, StringComparison.Ordinal);
            if (match < 0)
            {
                match = text.LastIndexOf(pattern, text.Length - 1, StringComparison.Ordinal);
            }
        }

        if (match < 0)
        {
            return false;
        }

        _editor.MoveCaret(match);
        return true;
    }
}
