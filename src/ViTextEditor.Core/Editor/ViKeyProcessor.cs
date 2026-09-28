namespace ViTextEditor.Core.Editor;

public sealed class ViKeyProcessor
{
    private readonly IEditorAdapter _editor;
    private readonly ViRegisterStore _registers;
    private string? _pending;
    private RepeatState? _lastRepeat;
    private PendingInsertRepeat? _pendingInsertRepeat;
    private bool _isRepeating;

    public ViKeyProcessor(IEditorAdapter editor, ViRegisterStore? registers = null)
    {
        _editor = editor;
        _registers = registers ?? new ViRegisterStore();
    }

    public EditorMode Mode { get; private set; } = EditorMode.Normal;
    public bool HasPendingCommand => _pending is not null;
    public bool IsRepeating => _isRepeating;
    public bool IsCapturingInsertRepeat => _pendingInsertRepeat is not null && !_isRepeating;
    public bool IsAwaitingReplaceCharacter => _pending == "r";

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
            case "~":
                if (ToggleCaseOrKana()) RecordRepeat(["~"]);
                return true;
            case "$":
                MoveLineEnd();
                return true;
            case "G":
                MoveLastLine();
                return true;
            case "J":
                if (JoinWithNextLine(insertSeparator: true)) RecordRepeat(["J"]);
                return true;
            case "Y":
                YankLines(1);
                return true;
            case "D":
                if (DeleteToLineEnd()) RecordRepeat(["D"]);
                return true;
            case "C":
            {
                var changed = ChangeToLineEnd();
                BeginInsertRepeat(["C"], changed);
                SetMode(EditorMode.Insert);
                return true;
            }
            case "g":
            case "d":
            case "y":
            case "c":
            case "r":
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
        if (pending == "r")
        {
            if (ReplaceCharacter(key)) RecordRepeat(["r", key]);
            return true;
        }

        if (pending == "g" && key == "g")
        {
            _editor.MoveCaret(0);
            return true;
        }

        if (pending == "g" && key == "J")
        {
            if (JoinWithNextLine(insertSeparator: false)) RecordRepeat(["g", "J"]);
            return true;
        }

        var operatorKey = pending[0];
        if (operatorKey is 'd' or 'y' or 'c' &&
            key.Length == 1 && char.IsAsciiDigit(key[0]) &&
            (pending.Length > 1 || key != "0"))
        {
            _pending = pending + key;
            return true;
        }

        var operatorCount = pending.Length > 1 &&
                            int.TryParse(pending.AsSpan(1), out var parsedCount)
            ? Math.Clamp(parsedCount, 1, 999_999)
            : 1;

        if (operatorKey == 'd')
        {
            bool changed;
            switch (key)
            {
                case "d":
                    changed = DeleteLines(operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "d"));
                    return true;
                case "w":
                    changed = DeleteWordMotion(bigWord: false, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "w"));
                    return true;
                case "W":
                    changed = DeleteWordMotion(bigWord: true, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "W"));
                    return true;
                case "e":
                    changed = DeleteToWordEnd(bigWord: false, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "e"));
                    return true;
                case "E":
                    changed = DeleteToWordEnd(bigWord: true, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "E"));
                    return true;
                case "b":
                    changed = DeletePreviousWords(bigWord: false, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "b"));
                    return true;
                case "B":
                    changed = DeletePreviousWords(bigWord: true, operatorCount);
                    if (changed) RecordRepeat(OperatorTokens('d', operatorCount, "B"));
                    return true;
                case "0":
                case "^":
                    changed = DeleteToLineStart(firstNonBlank: key == "^");
                    if (changed) RecordRepeat(["d", key]);
                    return true;
                case "$":
                    changed = DeleteToLineEnd();
                    if (changed) RecordRepeat(["d", "$"]);
                    return true;
            }
        }

        if (operatorKey == 'y')
        {
            if (key == "y")
            {
                YankLines(operatorCount);
                return true;
            }

            if (YankMotion(key, operatorCount)) return true;
        }

        if (operatorKey == 'c')
        {
            if (key == "c")
            {
                var changed = ChangeLines(operatorCount);
                BeginInsertRepeat(OperatorTokens('c', operatorCount, "c"), changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key is "w" or "e")
            {
                var changed = ChangeWordMotion(bigWord: false, operatorCount, nextWordMotion: key == "w");
                BeginInsertRepeat(OperatorTokens('c', operatorCount, key), changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key is "W" or "E")
            {
                var changed = ChangeWordMotion(bigWord: true, operatorCount, nextWordMotion: key == "W");
                BeginInsertRepeat(OperatorTokens('c', operatorCount, key), changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key is "b" or "B")
            {
                var changed = DeletePreviousWords(bigWord: key == "B", operatorCount);
                BeginInsertRepeat(OperatorTokens('c', operatorCount, key), changed);
                SetMode(EditorMode.Insert);
                return true;
            }

            if (key is "0" or "^")
            {
                var changed = DeleteToLineStart(firstNonBlank: key == "^");
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

    private static string[] OperatorTokens(char operation, int count, string motion) =>
        count == 1
            ? [operation.ToString(), motion]
            : [operation.ToString(), count.ToString(), motion];

    private void BeginInsertRepeat(string[] tokens, bool baseChanged)
    {
        if (_isRepeating) return;
        _pendingInsertRepeat = new PendingInsertRepeat(tokens, baseChanged);
    }

    private void RecordRepeat(string[] tokens)
    {
        if (_isRepeating) return;
        _lastRepeat = new RepeatState(tokens, []);
    }

    private void RepeatLastChange()
    {
        if (_lastRepeat is null || _isRepeating) return;

        var repeat = _lastRepeat;
        _isRepeating = true;
        _pending = null;
        _pendingInsertRepeat = null;
        try
        {
            foreach (var token in repeat.Tokens) Handle(token);

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
        if (Mode == mode) return;
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
        if (position < length && _editor.CharAt(position) is not ('\n' or '\r')) position++;
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

    private bool ReplaceCharacter(string replacement)
    {
        var isSingleCharacter = replacement.Length == 1 ||
                                replacement.Length == 2 && char.IsSurrogatePair(replacement[0], replacement[1]);
        if (!isSingleCharacter || replacement.Any(char.IsControl) || _editor.TextLength == 0) return false;
        var position = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        if (_editor.CharAt(position) is '\r' or '\n') return false;
        var next = _editor.NextCharacterPosition(position);
        if (next <= position) return false;
        _registers.StoreText(null, _editor.GetTextRange(position, next - position), linewise: false);
        _editor.ReplaceRange(position, next - position, replacement);
        _editor.MoveCaret(position);
        return true;
    }

    private bool ToggleCaseOrKana()
    {
        if (_editor.TextLength == 0) return false;
        var position = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var currentText = _editor.CharacterAt(position);
        if (currentText.Length == 0) return false;

        string? replacement = null;
        if (currentText.Length == 1 && char.IsAsciiLetter(currentText[0]))
        {
            replacement = (char.IsUpper(currentText[0])
                ? char.ToLowerInvariant(currentText[0])
                : char.ToUpperInvariant(currentText[0])).ToString();
        }
        else if (currentText.Length == 1 && TryToggleKana(currentText[0], out var convertedKana))
        {
            replacement = convertedKana.ToString();
        }

        var lineEnd = _editor.LineEndExclusive(position);
        var next = _editor.NextCharacterPosition(position);
        if (replacement is null)
        {
            _editor.MoveCaret(next < lineEnd ? next : position);
            return false;
        }
        _editor.ReplaceRange(position, next - position, replacement);
        _editor.MoveCaret(next < lineEnd ? next : position);
        return true;
    }

    private static bool TryToggleKana(char value, out char converted)
    {
        if (value is >= '\u3041' and <= '\u3096' or >= '\u309D' and <= '\u309F')
        {
            converted = (char)(value + 0x60);
            return true;
        }
        if (value is >= '\u30A1' and <= '\u30F6' or >= '\u30FD' and <= '\u30FF')
        {
            converted = (char)(value - 0x60);
            return true;
        }
        converted = value;
        return false;
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

    private bool JoinWithNextLine(bool insertSeparator)
    {
        var length = _editor.TextLength;
        if (length == 0) return false;

        var current = Math.Clamp(_editor.CaretPosition, 0, length - 1);
        var currentStart = _editor.LineStart(current);
        var currentEnd = _editor.LineEndExclusive(current);
        var nextStart = SkipLineBreak(currentEnd);
        if (nextStart <= currentEnd || nextStart >= length) return false;

        var deleteEnd = nextStart;
        var separator = string.Empty;
        if (insertSeparator)
        {
            var nextEnd = _editor.LineEndExclusive(nextStart);
            while (deleteEnd < nextEnd && _editor.CharAt(deleteEnd) is ' ' or '\t') deleteEnd++;

            var currentIsEmpty = currentEnd == currentStart;
            var nextIsEmpty = deleteEnd >= nextEnd;
            var currentEndsWithWhitespace = !currentIsEmpty && char.IsWhiteSpace(_editor.CharAt(currentEnd - 1));
            var nextStartsWithClosingParenthesis = !nextIsEmpty && _editor.CharAt(deleteEnd) == ')';
            if (!currentIsEmpty && !nextIsEmpty && !currentEndsWithWhitespace && !nextStartsWithClosingParenthesis)
            {
                separator = " ";
            }
        }

        _editor.ReplaceRange(currentEnd, deleteEnd - currentEnd, separator);
        _editor.MoveCaret(currentEnd);
        return true;
    }

    private bool ChangeWordMotion(bool bigWord, int count, bool nextWordMotion)
    {
        var length = _editor.TextLength;
        if (length == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, length - 1);
        int end;
        if (nextWordMotion && char.IsWhiteSpace(_editor.CharAt(start)))
        {
            end = start;
            for (var index = 0; index < Math.Max(1, count); index++)
            {
                var next = FindNextWordStart(end, bigWord);
                if (next <= end) break;
                end = next;
            }
        }
        else
        {
            // Vim treats cw/cW on a non-blank character like ce/cE.
            end = FindWordEndExclusive(start, bigWord, count);
        }
        var changed = DeleteIntoRegister(start, end);
        if (changed) _editor.MoveCaret(Math.Min(start, _editor.TextLength));
        return changed;
    }

    private bool ChangeToLineEnd()
    {
        if (_editor.TextLength == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var changed = DeleteIntoRegister(start, _editor.LineEndExclusive(start));
        if (changed) _editor.MoveCaret(Math.Min(start, _editor.TextLength));
        return changed;
    }

    private bool DeleteWordMotion(bool bigWord, int count)
    {
        if (_editor.TextLength == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var lineEnd = _editor.LineEndExclusive(start);
        var end = start;
        for (var index = 0; index < Math.Max(1, count); index++)
        {
            var next = FindNextWordStart(end, bigWord);
            if (next <= end) break;
            end = next;
        }
        if (end > lineEnd) end = lineEnd;
        if (end <= start) end = Math.Min(_editor.TextLength, start + 1);
        return DeleteIntoRegister(start, end);
    }

    private bool DeleteToWordEnd(bool bigWord, int count)
    {
        var length = _editor.TextLength;
        if (length == 0) return false;
        var start = Math.Clamp(_editor.CaretPosition, 0, length - 1);
        var end = FindWordEndExclusive(start, bigWord, count);
        return DeleteIntoRegister(start, end);
    }

    private int FindWordEndExclusive(int start, bool bigWord, int count)
    {
        var length = _editor.TextLength;
        var lineEnd = _editor.LineEndExclusive(start);
        var end = Math.Clamp(start, 0, length);
        for (var index = 0; index < Math.Max(1, count) && end < lineEnd; index++)
        {
            while (end < lineEnd && char.IsWhiteSpace(_editor.CharAt(end))) end++;
            if (end >= lineEnd) break;
            if (bigWord)
            {
                while (end < lineEnd && !char.IsWhiteSpace(_editor.CharAt(end))) end++;
            }
            else
            {
                var wordClass = ClassifySmallWord(_editor.CharAt(end));
                while (end < lineEnd && ClassifySmallWord(_editor.CharAt(end)) == wordClass) end++;
            }
        }
        return end;
    }

    private int FindPreviousWordStart(int start, bool bigWord, int count)
    {
        var lineStart = _editor.LineStart(start);
        var position = Math.Clamp(start, lineStart, _editor.TextLength);
        for (var index = 0; index < Math.Max(1, count) && position > lineStart; index++)
        {
            var cursor = position - 1;
            while (cursor >= lineStart && char.IsWhiteSpace(_editor.CharAt(cursor))) cursor--;
            if (cursor < lineStart) return lineStart;
            if (bigWord)
            {
                while (cursor > lineStart && !char.IsWhiteSpace(_editor.CharAt(cursor - 1))) cursor--;
            }
            else
            {
                var wordClass = ClassifySmallWord(_editor.CharAt(cursor));
                while (cursor > lineStart && ClassifySmallWord(_editor.CharAt(cursor - 1)) == wordClass) cursor--;
            }
            position = cursor;
        }
        return position;
    }

    private bool DeletePreviousWords(bool bigWord, int count)
    {
        if (_editor.TextLength == 0) return false;
        var end = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength);
        var start = FindPreviousWordStart(end, bigWord, count);
        return DeleteIntoRegister(start, end);
    }

    private int FirstNonBlankPosition(int position)
    {
        var start = _editor.LineStart(position);
        var end = _editor.LineEndExclusive(position);
        while (start < end && _editor.CharAt(start) is ' ' or '\t') start++;
        return start;
    }

    private bool DeleteToLineStart(bool firstNonBlank)
    {
        if (_editor.TextLength == 0) return false;
        var end = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength);
        var start = firstNonBlank ? FirstNonBlankPosition(end) : _editor.LineStart(end);
        if (start > end) (start, end) = (end, start);
        return DeleteIntoRegister(start, end);
    }

    private bool YankMotion(string motion, int count)
    {
        if (_editor.TextLength == 0) return false;
        var caret = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        int start;
        int end;
        switch (motion)
        {
            case "w":
            case "W":
                start = caret;
                end = caret;
                for (var index = 0; index < Math.Max(1, count); index++)
                {
                    var next = FindNextWordStart(end, motion == "W");
                    if (next <= end) break;
                    end = next;
                }
                end = Math.Min(end, _editor.LineEndExclusive(caret));
                break;
            case "e":
            case "E":
                start = caret;
                end = FindWordEndExclusive(caret, motion == "E", count);
                break;
            case "b":
            case "B":
                end = caret;
                start = FindPreviousWordStart(caret, motion == "B", count);
                break;
            case "0":
                end = caret;
                start = _editor.LineStart(caret);
                break;
            case "^":
                end = caret;
                start = FirstNonBlankPosition(caret);
                if (start > end) (start, end) = (end, start);
                break;
            case "$":
                start = caret;
                end = _editor.LineEndExclusive(caret);
                break;
            default:
                return false;
        }

        start = Math.Clamp(start, 0, _editor.TextLength);
        end = Math.Clamp(end, start, _editor.TextLength);
        if (end <= start) return true;
        _registers.StoreText(null, _editor.GetTextRange(start, end - start), linewise: false);
        return true;
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

        _registers.StoreText(null, _editor.GetTextRange(start, end - start), linewise: false);
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
        _registers.StoreText(null, _editor.GetTextRange(position, Math.Min(length, _editor.TextLength - position)), linewise: false);
        _editor.DeleteRange(position, Math.Min(length, _editor.TextLength - position));
        var remaining = _editor.TextLength;
        _editor.MoveCaret(remaining == 0 ? 0 : Math.Min(position, remaining - 1));
        return true;
    }

    private bool DeleteCurrentLine() => DeleteLines(1);

    private bool DeleteLines(int count)
    {
        if (_editor.TextLength == 0) return false;
        count = Math.Max(1, count);
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var start = _editor.LineStart(current);
        var scan = start;
        var lines = new List<string>(Math.Min(count, 1024));
        var afterBreak = start;
        for (var lineIndex = 0; lineIndex < count && scan < _editor.TextLength; lineIndex++)
        {
            var contentEnd = _editor.LineEndExclusive(scan);
            lines.Add(_editor.GetTextRange(scan, Math.Max(0, contentEnd - scan)));
            afterBreak = SkipLineBreak(contentEnd);
            if (afterBreak <= contentEnd) break;
            scan = afterBreak;
        }
        _registers.Yank(null, lines);

        int deleteStart;
        int deleteLength;
        if (afterBreak < _editor.TextLength)
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
        _editor.MoveCaret(remaining == 0 ? 0 : _editor.LineStart(Math.Min(start, remaining - 1)));
        return true;
    }

    private void YankCurrentLine() => YankLines(1);

    private void YankLines(int count)
    {
        if (_editor.TextLength == 0)
        {
            _registers.Yank(null, [string.Empty]);
            return;
        }
        count = Math.Max(1, count);
        var scan = _editor.LineStart(_editor.CaretPosition);
        var lines = new List<string>(Math.Min(count, 1024));
        for (var lineIndex = 0; lineIndex < count && scan < _editor.TextLength; lineIndex++)
        {
            var end = _editor.LineEndExclusive(scan);
            lines.Add(_editor.GetTextRange(scan, Math.Max(0, end - scan)));
            var next = SkipLineBreak(end);
            if (next <= end) break;
            scan = next;
        }
        _registers.Yank(null, lines);
    }

    private bool ChangeLines(int count)
    {
        if (_editor.TextLength == 0) return false;
        count = Math.Max(1, count);
        var start = _editor.LineStart(_editor.CaretPosition);
        var scan = start;
        var lines = new List<string>(Math.Min(count, 1024));
        var end = start;
        for (var lineIndex = 0; lineIndex < count && scan < _editor.TextLength; lineIndex++)
        {
            var contentEnd = _editor.LineEndExclusive(scan);
            lines.Add(_editor.GetTextRange(scan, Math.Max(0, contentEnd - scan)));
            end = contentEnd;
            if (lineIndex + 1 >= count) break;
            var next = SkipLineBreak(contentEnd);
            if (next <= contentEnd) break;
            end = next;
            scan = next;
        }

        _registers.Yank(null, lines);
        _editor.DeleteRange(start, Math.Max(0, end - start));
        _editor.MoveCaret(Math.Min(start, _editor.TextLength));
        return true;
    }

    private bool Paste(bool after)
    {
        if (!_registers.TryGetContent(null, out var register) || register.Parts.Count == 0) return false;

        var payload = register.ToText(DetectNewLine());
        if (payload.Length == 0 && !register.IsLinewise) return false;
        if (register.IsLinewise)
        {
            PasteLinewise(after, payload);
            return true;
        }

        var insertAt = _editor.TextLength == 0
            ? 0
            : after ? Math.Min(_editor.CaretPosition + 1, _editor.TextLength) : Math.Min(_editor.CaretPosition, _editor.TextLength);
        _editor.InsertText(insertAt, payload);
        _editor.MoveCaret(Math.Min(insertAt, Math.Max(0, _editor.TextLength - 1)));
        return true;
    }

    private void PasteLinewise(bool after, string payload)
    {
        var newlineText = DetectNewLine();
        if (_editor.TextLength == 0)
        {
            _editor.InsertText(0, payload);
            _editor.MoveCaret(0);
            return;
        }

        var start = _editor.LineStart(_editor.CaretPosition);
        if (!after)
        {
            _editor.InsertText(start, payload + newlineText);
            _editor.MoveCaret(start);
            return;
        }

        var end = _editor.LineEndExclusive(_editor.CaretPosition);
        var afterBreak = SkipLineBreak(end);
        if (afterBreak > end)
        {
            _editor.InsertText(afterBreak, payload + newlineText);
            _editor.MoveCaret(afterBreak);
        }
        else
        {
            var insertAt = _editor.TextLength;
            _editor.InsertText(insertAt, newlineText + payload);
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
