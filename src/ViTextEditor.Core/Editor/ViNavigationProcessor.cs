namespace ViTextEditor.Core.Editor;

public sealed class ViNavigationProcessor
{
    private readonly IEditorAdapter _editor;

    public ViNavigationProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public bool Handle(string key)
    {
        switch (key)
        {
            case "w":
                MoveNextWord(bigWord: false);
                return true;
            case "b":
                MovePreviousWord(bigWord: false);
                return true;
            case "W":
                MoveNextWord(bigWord: true);
                return true;
            case "B":
                MovePreviousWord(bigWord: true);
                return true;
            case "Ctrl+f":
                _editor.ScrollPage(1);
                return true;
            case "Ctrl+b":
                _editor.ScrollPage(-1);
                return true;
            case "Ctrl+d":
                _editor.ScrollHalfPage(1);
                return true;
            case "Ctrl+u":
                _editor.ScrollHalfPage(-1);
                return true;
            default:
                return false;
        }
    }

    private void MoveNextWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var i = current;

        if (char.IsWhiteSpace(text[i]))
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }
        else if (bigWord)
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }
        else
        {
            var wordClass = ClassifySmallWord(text[i]);
            while (i < text.Length && ClassifySmallWord(text[i]) == wordClass) i++;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }

        if (i < text.Length)
        {
            _editor.MoveCaret(i);
        }
    }

    private void MovePreviousWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var i = Math.Clamp(_editor.CaretPosition - 1, 0, text.Length - 1);
        while (i > 0 && char.IsWhiteSpace(text[i])) i--;

        if (bigWord)
        {
            while (i > 0 && !char.IsWhiteSpace(text[i - 1])) i--;
        }
        else
        {
            var wordClass = ClassifySmallWord(text[i]);
            while (i > 0 && ClassifySmallWord(text[i - 1]) == wordClass) i--;
        }

        _editor.MoveCaret(i);
    }

    private static SmallWordClass ClassifySmallWord(char c)
    {
        if (char.IsWhiteSpace(c)) return SmallWordClass.Whitespace;
        if (char.IsLetterOrDigit(c) || c == '_') return SmallWordClass.Keyword;
        return SmallWordClass.Punctuation;
    }

    private enum SmallWordClass
    {
        Whitespace,
        Keyword,
        Punctuation
    }
}
