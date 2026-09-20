namespace ViTextEditor.Core.Editor;

public sealed class ViKeyProcessor
{
    private readonly IEditorAdapter _editor;
    private string? _pending;
    private string _register = string.Empty;
    private bool _registerIsLinewise;

    public ViKeyProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public EditorMode Mode { get; private set; } = EditorMode.Normal;
    public bool HasPendingCommand => _pending is not null;

    public event EventHandler? ModeChanged;

    public bool Handle(string key)
    {
        if (Mode == EditorMode.Insert)
        {
            if (!string.Equals(key, "Esc", StringComparison.Ordinal))
            {
                // INSERT中の通常入力はScintilla/IMEへそのまま渡す。
                return false;
            }

            NormalizeNormalCaret();
            SetMode(EditorMode.Normal);
            _pending = null;
            return true;
        }

        if (string.Equals(key, "Esc", StringComparison.Ordinal))
        {
            _pending = null;
            return true;
        }

        if (_pending is not null)
        {
            var pending = _pending;
            _pending = null;

            if (pending == "g" && key == "g")
            {
                _editor.MoveCaret(0);
                return true;
            }

            if (pending == "d" && key == "d")
            {
                DeleteCurrentLine();
                return true;
            }

            if (pending == "y" && key == "y")
            {
                YankCurrentLine();
                return true;
            }

            if (pending == "c" && key is "w" or "e")
            {
                ChangeWord(bigWord: false);
                return true;
            }

            if (pending == "c" && key == "W")
            {
                ChangeWord(bigWord: true);
                return true;
            }

            if (pending == "c" && key == "$")
            {
                ChangeToLineEnd();
                return true;
            }

            // 未対応の複合コマンドは文字を挿入せず消費する。
            return true;
        }

        switch (key)
        {
            case "i":
                SetMode(EditorMode.Insert);
                return true;
            case "a":
                MoveAfterCaretForInsert();
                SetMode(EditorMode.Insert);
                return true;
            case "o":
                OpenLineBelow();
                SetMode(EditorMode.Insert);
                return true;
            case "O":
                OpenLineAbove();
                SetMode(EditorMode.Insert);
                return true;
            case "h":
                MoveHorizontal(-1);
                return true;
            case "l":
                MoveHorizontal(1);
                return true;
            case "j":
                MoveVertical(1);
                return true;
            case "k":
                MoveVertical(-1);
                return true;
            case "w":
                MoveNextWord();
                return true;
            case "b":
                MovePreviousWord();
                return true;
            case "e":
                MoveEndWord();
                return true;
            case "0":
                _editor.MoveCaret(LineStart(_editor.Text, _editor.CaretPosition));
                return true;
            case "^":
                MoveFirstNonWhitespace();
                return true;
            case "$":
                MoveLineEnd();
                return true;
            case "G":
                MoveLastLine();
                return true;
            case "g":
            case "d":
            case "y":
            case "c":
                _pending = key;
                return true;
            case "x":
                DeleteCharacter();
                return true;
            case "p":
                Paste(after: true);
                return true;
            case "P":
                Paste(after: false);
                return true;
            case "u":
                _editor.Undo();
                return true;
            case "Ctrl+r":
                _editor.Redo();
                return true;
            default:
                // NORMALモードでは未対応の印字キーをScintillaへ流さない。
                return key.Length == 1;
        }
    }

    private void SetMode(EditorMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NormalizeNormalCaret()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        var position = Math.Min(_editor.CaretPosition, text.Length - 1);
        if (position > 0 && text[position] == '\n')
        {
            position--;
        }
        if (position > 0 && text[position] == '\r')
        {
            position--;
        }

        _editor.MoveCaret(Math.Max(0, position));
    }

    private void MoveAfterCaretForInsert()
    {
        var text = _editor.Text;
        var position = Math.Clamp(_editor.CaretPosition, 0, text.Length);
        if (position < text.Length && text[position] != '\n' && text[position] != '\r')
        {
            position++;
        }
        _editor.MoveCaret(position);
    }

    private void MoveHorizontal(int delta)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var start = LineStart(text, current);
        var end = LineEndExclusive(text, current);
        var max = end > start ? end - 1 : start;
        _editor.MoveCaret(Math.Clamp(current + delta, start, max));
    }

    private void MoveVertical(int direction)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var currentStart = LineStart(text, current);
        var column = Math.Max(0, current - currentStart);

        int targetStart;
        if (direction > 0)
        {
            var nextBreak = text.IndexOf('\n', currentStart);
            if (nextBreak < 0 || nextBreak + 1 >= text.Length)
            {
                return;
            }
            targetStart = nextBreak + 1;
        }
        else
        {
            if (currentStart == 0)
            {
                return;
            }
            var searchFrom = Math.Max(0, currentStart - 2);
            var previousBreak = text.LastIndexOf('\n', searchFrom);
            targetStart = previousBreak + 1;
        }

        var targetEnd = LineEndExclusive(text, targetStart);
        var maxColumn = Math.Max(0, targetEnd - targetStart - 1);
        _editor.MoveCaret(targetStart + Math.Min(column, maxColumn));
    }

    private void MoveNextWord()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var i = Math.Min(current + 1, text.Length);
        if (IsWord(text[current]))
        {
            while (i < text.Length && IsWord(text[i])) i++;
        }
        while (i < text.Length && !IsWord(text[i])) i++;
        if (i < text.Length) _editor.MoveCaret(i);
    }

    private void MovePreviousWord()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var i = Math.Clamp(_editor.CaretPosition - 1, 0, text.Length - 1);
        while (i > 0 && !IsWord(text[i])) i--;
        while (i > 0 && IsWord(text[i - 1])) i--;
        _editor.MoveCaret(i);
    }

    private void MoveEndWord()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var i = Math.Clamp(_editor.CaretPosition + 1, 0, text.Length - 1);
        while (i < text.Length - 1 && !IsWord(text[i])) i++;
        while (i < text.Length - 1 && IsWord(text[i + 1])) i++;
        _editor.MoveCaret(i);
    }

    private void MoveFirstNonWhitespace()
    {
        var text = _editor.Text;
        var start = LineStart(text, _editor.CaretPosition);
        var end = LineEndExclusive(text, _editor.CaretPosition);
        var i = start;
        while (i < end && char.IsWhiteSpace(text[i])) i++;
        _editor.MoveCaret(i < end ? i : start);
    }

    private void MoveLineEnd()
    {
        var text = _editor.Text;
        var start = LineStart(text, _editor.CaretPosition);
        var end = LineEndExclusive(text, _editor.CaretPosition);
        _editor.MoveCaret(end > start ? end - 1 : start);
    }

    private void MoveLastLine()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            _editor.MoveCaret(0);
            return;
        }
        var lastBreak = text.LastIndexOf('\n');
        _editor.MoveCaret(lastBreak < 0 ? 0 : Math.Min(lastBreak + 1, text.Length - 1));
    }

    private void ChangeWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            SetMode(EditorMode.Insert);
            return;
        }

        var start = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var lineEnd = LineEndExclusive(text, start);
        if (start >= lineEnd)
        {
            SetMode(EditorMode.Insert);
            return;
        }

        var end = start;
        if (char.IsWhiteSpace(text[start]))
        {
            while (end < lineEnd && char.IsWhiteSpace(text[end])) end++;
        }
        else if (bigWord)
        {
            while (end < lineEnd && !char.IsWhiteSpace(text[end])) end++;
        }
        else
        {
            var wordClass = ClassifySmallWord(text[start]);
            while (end < lineEnd && ClassifySmallWord(text[end]) == wordClass) end++;
        }

        if (end > start)
        {
            _register = text.Substring(start, end - start);
            _registerIsLinewise = false;
            _editor.DeleteRange(start, end - start);
            _editor.MoveCaret(start);
        }

        SetMode(EditorMode.Insert);
    }

    private void ChangeToLineEnd()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            SetMode(EditorMode.Insert);
            return;
        }

        var start = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var end = LineEndExclusive(text, start);
        if (end > start)
        {
            _register = text.Substring(start, end - start);
            _registerIsLinewise = false;
            _editor.DeleteRange(start, end - start);
            _editor.MoveCaret(start);
        }

        SetMode(EditorMode.Insert);
    }

    private void DeleteCharacter()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var position = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var length = text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n' ? 2 : 1;
        _register = text.Substring(position, Math.Min(length, text.Length - position));
        _registerIsLinewise = false;
        _editor.DeleteRange(position, Math.Min(length, text.Length - position));
        var remaining = _editor.Text.Length;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(position, remaining - 1));
    }

    private void DeleteCurrentLine()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var start = LineStart(text, current);
        var contentEnd = LineEndExclusive(text, current);
        _register = text.Substring(start, Math.Max(0, contentEnd - start));
        _registerIsLinewise = true;

        var newline = text.IndexOf('\n', start);
        int deleteStart;
        int deleteLength;
        if (newline >= 0)
        {
            deleteStart = start;
            deleteLength = newline + 1 - start;
        }
        else if (start > 0)
        {
            var breakLength = start >= 2 && text[start - 2] == '\r' && text[start - 1] == '\n' ? 2 : 1;
            deleteStart = start - breakLength;
            deleteLength = text.Length - deleteStart;
        }
        else
        {
            deleteStart = 0;
            deleteLength = text.Length;
        }

        _editor.DeleteRange(deleteStart, deleteLength);
        var remaining = _editor.Text.Length;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(deleteStart, remaining - 1));
    }

    private void YankCurrentLine()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            _register = string.Empty;
            _registerIsLinewise = true;
            return;
        }

        var start = LineStart(text, _editor.CaretPosition);
        var end = LineEndExclusive(text, _editor.CaretPosition);
        _register = text.Substring(start, Math.Max(0, end - start));
        _registerIsLinewise = true;
    }

    private void Paste(bool after)
    {
        if (_register.Length == 0)
        {
            return;
        }

        var text = _editor.Text;
        if (_registerIsLinewise)
        {
            PasteLinewise(text, after);
            return;
        }

        var insertAt = text.Length == 0
            ? 0
            : after ? Math.Min(_editor.CaretPosition + 1, text.Length) : Math.Min(_editor.CaretPosition, text.Length);
        _editor.InsertText(insertAt, _register);
        _editor.MoveCaret(Math.Min(insertAt, Math.Max(0, _editor.Text.Length - 1)));
    }

    private void PasteLinewise(string text, bool after)
    {
        if (text.Length == 0)
        {
            _editor.InsertText(0, _register);
            _editor.MoveCaret(0);
            return;
        }

        var newlineText = DetectNewLine(text);
        var start = LineStart(text, _editor.CaretPosition);
        if (!after)
        {
            _editor.InsertText(start, _register + newlineText);
            _editor.MoveCaret(start);
            return;
        }

        var newline = text.IndexOf('\n', start);
        if (newline >= 0)
        {
            var insertAt = newline + 1;
            _editor.InsertText(insertAt, _register + newlineText);
            _editor.MoveCaret(insertAt);
        }
        else
        {
            var insertAt = text.Length;
            _editor.InsertText(insertAt, newlineText + _register);
            _editor.MoveCaret(insertAt + newlineText.Length);
        }
    }

    private void OpenLineBelow()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var start = LineStart(text, _editor.CaretPosition);
        var newlineText = DetectNewLine(text);
        var newline = text.IndexOf('\n', start);
        if (newline >= 0)
        {
            var insertAt = newline + 1;
            _editor.InsertText(insertAt, newlineText);
            _editor.MoveCaret(insertAt);
        }
        else
        {
            var insertAt = text.Length;
            _editor.InsertText(insertAt, newlineText);
            _editor.MoveCaret(insertAt + newlineText.Length);
        }
    }

    private void OpenLineAbove()
    {
        var text = _editor.Text;
        if (text.Length == 0)
        {
            return;
        }

        var start = LineStart(text, _editor.CaretPosition);
        _editor.InsertText(start, DetectNewLine(text));
        _editor.MoveCaret(start);
    }

    private static SmallWordClass ClassifySmallWord(char c)
    {
        if (char.IsWhiteSpace(c)) return SmallWordClass.Whitespace;
        if (IsWord(c)) return SmallWordClass.Keyword;
        return SmallWordClass.Punctuation;
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int LineStart(string text, int position)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0)
        {
            return 0;
        }
        var previousBreak = text.LastIndexOf('\n', Math.Min(position - 1, text.Length - 1));
        return previousBreak + 1;
    }

    private static int LineEndExclusive(string text, int position)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var start = LineStart(text, position);
        var newline = text.IndexOf('\n', start);
        var end = newline < 0 ? text.Length : newline;
        if (end > start && text[end - 1] == '\r')
        {
            end--;
        }
        return end;
    }

    private static string DetectNewLine(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (text.Contains('\n')) return "\n";
        if (text.Contains('\r')) return "\r";
        return Environment.NewLine;
    }

    private enum SmallWordClass
    {
        Whitespace,
        Keyword,
        Punctuation
    }
}
