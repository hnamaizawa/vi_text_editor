namespace ViTextEditor.Core.Editor;

public sealed class ViKeyProcessor
{
    private readonly IEditorAdapter _editor;
    private string? _pending;
    private string _register = string.Empty;
    private bool _registerIsLinewise;
    private RepeatState? _lastRepeat;
    private PendingInsertRepeat? _pendingInsertRepeat;
    private bool _isRepeating;

    public ViKeyProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public EditorMode Mode { get; private set; } = EditorMode.Normal;
    public bool HasPendingCommand => _pending is not null;
    public bool IsRepeating => _isRepeating;
    public bool IsCapturingInsertRepeat => _pendingInsertRepeat is not null && !_isRepeating;

    public event EventHandler? ModeChanged;

    public void CommitInsertRepeat(IReadOnlyList<ViRepeatEdit> edits)
    {
        if (_pendingInsertRepeat is null || _isRepeating)
        {
            return;
        }

        var pending = _pendingInsertRepeat;
        _pendingInsertRepeat = null;
        if (!pending.BaseChanged && edits.Count == 0)
        {
            return;
        }

        _lastRepeat = new RepeatState(
            pending.Tokens.ToArray(),
            edits.Select(edit => new ViRepeatEdit(edit.IsInsert, edit.RelativePosition, edit.Text)).ToArray());
    }

    public bool Handle(string key)
    {
        if (Mode == EditorMode.Insert)
        {
            if (!string.Equals(key, "Esc", StringComparison.Ordinal))
            {
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
            return HandlePending(pending, key);
        }

        switch (key)
        {
            case ".":
                RepeatLastChange();
                return true;
            case "i":
                BeginInsertRepeat(["i"], baseChanged: false);
                SetMode(EditorMode.Insert);
                return true;
            case "a":
                MoveAfterCaretForInsert();
                BeginInsertRepeat(["a"], baseChanged: false);
                SetMode(EditorMode.Insert);
                return true;
            case "o":
            {
                var changed = OpenLineBelow();
                BeginInsertRepeat(["o"], changed);
                SetMode(EditorMode.Insert);
                return true;
            }
            case "O":
            {
                var changed = OpenLineAbove();
                BeginInsertRepeat(["O"], changed);
                SetMode(EditorMode.Insert);
                return true;
            }
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
                MoveNextWord(bigWord: false);
                return true;
            case "W":
                MoveNextWord(bigWord: true);
                return true;
            case "b":
                MovePreviousWord(bigWord: false);
                return true;
            case "B":
                MovePreviousWord(bigWord: true);
                return true;
            case "e":
                MoveEndWord(bigWord: false);
                return true;
            case "E":
                MoveEndWord(bigWord: true);
                return true;
            case "0":
                _editor.MoveCaret(_editor.LineStart(_editor.CaretPosition));
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
            case "D":
                if (DeleteToLineEnd()) RecordRepeat(["D"]);
                return true;
            case "g":
            case "d":
            case "y":
            case "c":
                _pending = key;
                return true;
            case "x":
                if (DeleteCharacter()) RecordRepeat(["x"]);
                return true;
            case "p":
                if (Paste(after: true)) RecordRepeat(["p"]);
                return true;
            case "P":
                if (Paste(after: false)) RecordRepeat(["P"]);
                return true;
            case "u":
                _editor.Undo();
                return true;
            case "Ctrl+r":
                _editor.Redo();
                return true;
            default:
                return key.Length == 1;
        }
    }

    private bool HandlePending(string pending, string key)
    {
        if (pending == "g" && key == "g")
        {
            _editor.MoveCaret(0);
            return true;
        }

        if (pending == "d")
        {
            bool changed;
            switch (key)
            {
                case "d":
                    changed = DeleteCurrentLine();
                    if (changed) RecordRepeat(["d", "d"]);
                    return true;
                case "w":
                    changed = DeleteWordMotion(bigWord: false);
                    if (changed) RecordRepeat(["d", "w"]);
                    return true;
                case "W":
                    changed = DeleteWordMotion(bigWord: true);
                    if (changed) RecordRepeat(["d", "W"]);
                    return true;
                case "e":
                    changed = DeleteToWordEnd(bigWord: false);
                    if (changed) RecordRepeat(["d", "e"]);
                    return true;
                case "E":
                    changed = DeleteToWordEnd(bigWord: true);
                    if (changed) RecordRepeat(["d", "E"]);
                    return true;
                case "$":
                    changed = DeleteToLineEnd();
                    if (changed) RecordRepeat(["d", "$"]);
                    return true;
            }
        }

        if (pending == "y" && key == "y")
        {
            YankCurrentLine();
            return true;
        }

        if (pending == "c")
        {
            if (key is "w" or "e")
            {
                var changed = ChangeWord(bigWord: false);
                BeginInsertRepeat(["c", key], changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key is "W" or "E")
            {
                var changed = ChangeWord(bigWord: true);
                BeginInsertRepeat(["c", key], changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key == "$")
            {
                var changed = ChangeToLineEnd();
                BeginInsertRepeat(["c", "$"], changed);
                SetMode(EditorMode.Insert);
                return true;
            }
        }

        return true;
    }

    private void BeginInsertRepeat(string[] tokens, bool baseChanged)
    {
        if (_isRepeating)
        {
            return;
        }
        _pendingInsertRepeat = new PendingInsertRepeat(tokens, baseChanged);
    }

    private void RecordRepeat(string[] tokens)
    {
        if (_isRepeating)
        {
            return;
        }
        _lastRepeat = new RepeatState(tokens, []);
    }

    private void RepeatLastChange()
    {
        if (_lastRepeat is null || _isRepeating)
        {
            return;
        }

        var repeat = _lastRepeat;
        _isRepeating = true;
        _pending = null;
        _pendingInsertRepeat = null;
        try
        {
            foreach (var token in repeat.Tokens)
            {
                Handle(token);
            }

            if (Mode == EditorMode.Insert)
            {
                var anchor = _editor.CaretPosition;
                foreach (var edit in repeat.InsertEdits)
                {
                    var position = Math.Clamp(anchor + edit.RelativePosition, 0, _editor.TextLength);
                    if (edit.IsInsert)
                    {
                        _editor.InsertText(position, edit.Text);
                    }
                    else
                    {
                        var length = Math.Min(edit.Text.Length, Math.Max(0, _editor.TextLength - position));
                        if (length > 0)
                        {
                            _editor.DeleteRange(position, length);
                            _editor.MoveCaret(position);
                        }
                    }
                }
                Handle("Esc");
            }
        }
        finally
        {
            _pending = null;
            _pendingInsertRepeat = null;
            _isRepeating = false;
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
        var length = _editor.TextLength;
        if (length == 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        var position = Math.Min(_editor.CaretPosition, length - 1);
        if (position > 0 && _editor.CharAt(position) == '\n') position--;
        if (position > 0 && _editor.CharAt(position) == '\r') position--;
        _editor.MoveCaret(Math.Max(0, position));
    }

    private void MoveAfterCaretForInsert()
    {
        var length = _editor.TextLength;
        var position = Math.Clamp(_editor.CaretPosition, 0, length);
        if (position < length && _editor.CharAt(position) is not ('\n' or '\r'))
        {
            position++;
        }
        _editor.MoveCaret(position);
    }

    private void MoveHorizontal(int delta)
    {
        if (_editor.TextLength == 0) return;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var start = _editor.LineStart(current);
        var end = _editor.LineEndExclusive(current);
        var max = end > start ? end - 1 : start;
        _editor.MoveCaret(Math.Clamp(current + delta, start, max));
    }

    private void MoveVertical(int direction)
    {
        if (_editor.TextLength == 0) return;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var currentStart = _editor.LineStart(current);
        var column = Math.Max(0, current - currentStart);

        if (direction > 0)
        {
            var end = _editor.LineEndExclusive(current);
            var next = SkipLineBreak(end);
            if (next >= _editor.TextLength) return;
            var targetEnd = _editor.LineEndExclusive(next);
            _editor.MoveCaret(next + Math.Min(column, Math.Max(0, targetEnd - next - 1)));
        }
        else
        {
            if (currentStart == 0) return;
            var previousEnd = currentStart - 1;
            if (previousEnd > 0 && _editor.CharAt(previousEnd) == '\n') previousEnd--;
            if (previousEnd > 0 && _editor.CharAt(previousEnd) == '\r') previousEnd--;
            var previousStart = _editor.LineStart(Math.Max(0, previousEnd));
            var targetEnd = _editor.LineEndExclusive(previousStart);
            _editor.MoveCaret(previousStart + Math.Min(column, Math.Max(0, targetEnd - previousStart - 1)));
        }
    }

    private void MoveNextWord(bool bigWord)
    {
        if (_editor.TextLength == 0) return;
        var target = FindNextWordStart(Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1), bigWord);
        if (target < _editor.TextLength) _editor.MoveCaret(target);
    }

    private int FindNextWordStart(int start, bool bigWord)
    {
        var length = _editor.TextLength;
        if (length == 0) return 0;
        var i = Math.Clamp(start, 0, length - 1);

        if (char.IsWhiteSpace(_editor.CharAt(i)))
        {
            while (i < length && char.IsWhiteSpace(_editor.CharAt(i))) i++;
            return i;
        }

        if (bigWord)
        {
            while (i < length && !char.IsWhiteSpace(_editor.CharAt(i))) i++;
        }
        else
        {
            var wordClass = ClassifySmallWord(_editor.CharAt(i));
            while (i < length && ClassifySmallWord(_editor.CharAt(i)) == wordClass) i++;
        }
        while (i < length && char.IsWhiteSpace(_editor.CharAt(i))) i++;
        return i;
    }

    private void MovePreviousWord(bool bigWord)
    {
        if (_editor.TextLength == 0) return;
        var i = Math.Clamp(_editor.CaretPosition - 1, 0, _editor.TextLength - 1);
        while (i > 0 && char.IsWhiteSpace(_editor.CharAt(i))) i--;
        if (bigWord)
        {
            while (i > 0 && !char.IsWhiteSpace(_editor.CharAt(i - 1))) i--;
        }
        else
        {
            var wordClass = ClassifySmallWord(_editor.CharAt(i));
            while (i > 0 && ClassifySmallWord(_editor.CharAt(i - 1)) == wordClass) i--;
        }
        _editor.MoveCaret(i);
    }

    private void MoveEndWord(bool bigWord)
    {
        var length = _editor.TextLength;
        if (length == 0) return;
        var i = Math.Clamp(_editor.CaretPosition + 1, 0, length - 1);
        while (i < length - 1 && char.IsWhiteSpace(_editor.CharAt(i))) i++;
        if (bigWord)
        {
            while (i < length - 1 && !char.IsWhiteSpace(_editor.CharAt(i + 1))) i++;
        }
        else
        {
            var wordClass = ClassifySmallWord(_editor.CharAt(i));
            while (i < length - 1 && ClassifySmallWord(_editor.CharAt(i + 1)) == wordClass) i++;
        }
        _editor.MoveCaret(i);
    }

    private void MoveFirstNonWhitespace()
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        var i = start;
        while (i < end && char.IsWhiteSpace(_editor.CharAt(i))) i++;
        _editor.MoveCaret(i < end ? i : start);
    }

    private void MoveLineEnd()
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        _editor.MoveCaret(end > start ? end - 1 : start);
    }

    private void MoveLastLine()
    {
        var length = _editor.TextLength;
        if (length == 0)
        {
            _editor.MoveCaret(0);
            return;
        }
        _editor.MoveCaret(_editor.LineStart(length - 1));
    }

    private bool ChangeWord(bool bigWord)
    {
        var length = _editor.TextLength;
        if (length == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, length - 1);
        var end = start;
        if (char.IsWhiteSpace(_editor.CharAt(start)))
        {
            while (end < length && char.IsWhiteSpace(_editor.CharAt(end))) end++;
        }
        else if (bigWord)
        {
            while (end < length && !char.IsWhiteSpace(_editor.CharAt(end))) end++;
        }
        else
        {
            var wordClass = ClassifySmallWord(_editor.CharAt(start));
            while (end < length && ClassifySmallWord(_editor.CharAt(end)) == wordClass) end++;
        }
        return DeleteIntoRegister(start, end);
    }

    private bool ChangeToLineEnd()
    {
        if (_editor.TextLength == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        return DeleteIntoRegister(start, _editor.LineEndExclusive(start));
    }

    private bool DeleteWordMotion(bool bigWord)
    {
        if (_editor.TextLength == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var end = FindNextWordStart(start, bigWord);
        if (end <= start)
        {
            end = Math.Min(_editor.TextLength, start + 1);
        }
        return DeleteIntoRegister(start, end);
    }

    private bool DeleteToWordEnd(bool bigWord)
    {
        var length = _editor.TextLength;
        if (length == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, length - 1);
        var end = start;
        while (end < length && char.IsWhiteSpace(_editor.CharAt(end))) end++;
        if (end >= length) return DeleteIntoRegister(start, length);
        if (bigWord)
        {
            while (end < length && !char.IsWhiteSpace(_editor.CharAt(end))) end++;
        }
        else
        {
            var wordClass = ClassifySmallWord(_editor.CharAt(end));
            while (end < length && ClassifySmallWord(_editor.CharAt(end)) == wordClass) end++;
        }
        return DeleteIntoRegister(start, end);
    }

    private bool DeleteToLineEnd()
    {
        if (_editor.TextLength == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        return DeleteIntoRegister(start, _editor.LineEndExclusive(start));
    }

    private bool DeleteIntoRegister(int start, int end)
    {
        var length = _editor.TextLength;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, start, length);
        if (end <= start) return false;

        _register = _editor.GetTextRange(start, end - start);
        _registerIsLinewise = false;
        _editor.DeleteRange(start, end - start);
        var remaining = _editor.TextLength;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(start, remaining - 1));
        return true;
    }

    private bool DeleteCharacter()
    {
        if (_editor.TextLength == 0) return false;
        var position = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var length = _editor.CharAt(position) == '\r' && position + 1 < _editor.TextLength && _editor.CharAt(position + 1) == '\n' ? 2 : 1;
        _register = _editor.GetTextRange(position, Math.Min(length, _editor.TextLength - position));
        _registerIsLinewise = false;
        _editor.DeleteRange(position, Math.Min(length, _editor.TextLength - position));
        var remaining = _editor.TextLength;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(position, remaining - 1));
        return true;
    }

    private bool DeleteCurrentLine()
    {
        if (_editor.TextLength == 0) return false;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var start = _editor.LineStart(current);
        var contentEnd = _editor.LineEndExclusive(current);
        _register = _editor.GetTextRange(start, Math.Max(0, contentEnd - start));
        _registerIsLinewise = true;

        var afterBreak = SkipLineBreak(contentEnd);
        int deleteStart;
        int deleteLength;
        if (afterBreak > contentEnd)
        {
            deleteStart = start;
            deleteLength = afterBreak - start;
        }
        else if (start > 0)
        {
            deleteStart = start - 1;
            if (deleteStart > 0 && _editor.CharAt(deleteStart) == '\n' && _editor.CharAt(deleteStart - 1) == '\r') deleteStart--;
            deleteLength = _editor.TextLength - deleteStart;
        }
        else
        {
            deleteStart = 0;
            deleteLength = _editor.TextLength;
        }

        _editor.DeleteRange(deleteStart, deleteLength);
        var remaining = _editor.TextLength;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(deleteStart, remaining - 1));
        return true;
    }

    private void YankCurrentLine()
    {
        if (_editor.TextLength == 0)
        {
            _register = string.Empty;
            _registerIsLinewise = true;
            return;
        }
        var start = _editor.LineStart(_editor.CaretPosition);
        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        _register = _editor.GetTextRange(start, Math.Max(0, end - start));
        _registerIsLinewise = true;
    }

    private bool Paste(bool after)
    {
        if (_register.Length == 0) return false;
        if (_registerIsLinewise)
        {
            PasteLinewise(after);
            return true;
        }

        var insertAt = _editor.TextLength == 0
            ? 0
            : after ? Math.Min(_editor.CaretPosition + 1, _editor.TextLength) : Math.Min(_editor.CaretPosition, _editor.TextLength);
        _editor.InsertText(insertAt, _register);
        _editor.MoveCaret(Math.Min(insertAt, Math.Max(0, _editor.TextLength - 1)));
        return true;
    }

    private void PasteLinewise(bool after)
    {
        var newlineText = DetectNewLine();
        if (_editor.TextLength == 0)
        {
            _editor.InsertText(0, _register);
            _editor.MoveCaret(0);
            return;
        }

        var start = _editor.LineStart(_editor.CaretPosition);
        if (!after)
        {
            _editor.InsertText(start, _register + newlineText);
            _editor.MoveCaret(start);
            return;
        }

        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        var afterBreak = SkipLineBreak(end);
        if (afterBreak > end)
        {
            _editor.InsertText(afterBreak, _register + newlineText);
            _editor.MoveCaret(afterBreak);
        }
        else
        {
            var insertAt = _editor.TextLength;
            _editor.InsertText(insertAt, newlineText + _register);
            _editor.MoveCaret(insertAt + newlineText.Length);
        }
    }

    private bool OpenLineBelow()
    {
        var newlineText = DetectNewLine();
        if (_editor.TextLength == 0)
        {
            _editor.InsertText(0, newlineText);
            _editor.MoveCaret(0);
            return true;
        }
        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        var afterBreak = SkipLineBreak(end);
        if (afterBreak > end)
        {
            _editor.InsertText(afterBreak, newlineText);
            _editor.MoveCaret(afterBreak);
        }
        else
        {
            var insertAt = _editor.TextLength;
            _editor.InsertText(insertAt, newlineText);
            _editor.MoveCaret(insertAt + newlineText.Length);
        }
        return true;
    }

    private bool OpenLineAbove()
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        _editor.InsertText(start, DetectNewLine());
        _editor.MoveCaret(start);
        return true;
    }

    private int SkipLineBreak(int position)
    {
        var i = Math.Clamp(position, 0, _editor.TextLength);
        if (i < _editor.TextLength && _editor.CharAt(i) == '\r') i++;
        if (i < _editor.TextLength && _editor.CharAt(i) == '\n') i++;
        return i;
    }

    private string DetectNewLine()
    {
        var sampleLength = Math.Min(_editor.TextLength, 4096);
        if (sampleLength == 0) return Environment.NewLine;
        var sample = _editor.GetTextRange(0, sampleLength);
        if (sample.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (sample.Contains('\n')) return "\n";
        if (sample.Contains('\r')) return "\r";
        return Environment.NewLine;
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

    private sealed record RepeatState(string[] Tokens, ViRepeatEdit[] InsertEdits);
    private sealed record PendingInsertRepeat(string[] Tokens, bool BaseChanged);
}
