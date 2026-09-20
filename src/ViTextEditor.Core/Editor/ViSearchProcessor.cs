namespace ViTextEditor.Core.Editor;

public sealed class ViSearchProcessor
{
    private readonly IEditorAdapter _editor;
    private readonly ViOptions _options;
    private string? _lastPattern;
    private int _lastDirection = 1;

    public ViSearchProcessor(IEditorAdapter editor, ViOptions? options = null)
    {
        _editor = editor;
        _options = options ?? ViOptions.Shared;
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

    private bool Find(string rawPattern, int direction)
    {
        var text = _editor.Text;
        if (text.Length == 0 || rawPattern.Length == 0)
        {
            return false;
        }

        var (pattern, ignoreCase) = ResolveCaseOverride(rawPattern);
        if (pattern.Length == 0) return false;
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        int match;
        if (direction > 0)
        {
            var start = Math.Min(_editor.CaretPosition + 1, text.Length);
            match = start < text.Length
                ? text.IndexOf(pattern, start, comparison)
                : -1;
            if (match < 0)
            {
                match = text.IndexOf(pattern, 0, comparison);
            }
        }
        else
        {
            var start = _editor.CaretPosition <= 0
                ? text.Length - 1
                : Math.Min(_editor.CaretPosition - 1, text.Length - 1);
            match = text.LastIndexOf(pattern, start, comparison);
            if (match < 0)
            {
                match = text.LastIndexOf(pattern, text.Length - 1, comparison);
            }
        }

        if (match < 0)
        {
            return false;
        }

        _editor.MoveCaret(match);
        return true;
    }

    private (string Pattern, bool IgnoreCase) ResolveCaseOverride(string pattern)
    {
        var ignoreCase = _options.IgnoreCase;
        var builder = new System.Text.StringBuilder(pattern.Length);
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '\\' && i + 1 < pattern.Length && pattern[i + 1] is 'c' or 'C')
            {
                ignoreCase = pattern[i + 1] == 'c';
                i++;
                continue;
            }
            builder.Append(pattern[i]);
        }
        return (builder.ToString(), ignoreCase);
    }
}
