namespace ViTextEditor.Core.Editor;

public sealed class ViNavigationProcessor
{
    private readonly IEditorAdapter _editor;
    private int _count;
    private bool _hasCount;
    private string? _pendingPrefix;
    private PendingFind? _pendingFind;
    private FindMotion? _lastFind;

    public ViNavigationProcessor(IEditorAdapter editor)
    {
        _editor = editor;
    }

    public bool IsAwaitingCharacter => _pendingFind is not null;
    public bool HasPendingMotion => _pendingPrefix is not null || _pendingFind is not null || _hasCount;

    public bool Handle(string key)
    {
        if (string.Equals(key, "Esc", StringComparison.Ordinal))
        {
            ResetPending();
            return true;
        }

        if (_pendingFind is not null)
        {
            return true;
        }

        if (_pendingPrefix == "g")
        {
            _pendingPrefix = null;
            if (key == "g")
            {
                var count = TakeCount(out var explicitCount);
                MoveToLine(explicitCount ? count : 1);
                return true;
            }
            if (key is "e" or "E")
            {
                var count = TakeCount(out _);
                Repeat(count, () => MovePreviousWordEnd(bigWord: key == "E"));
                return true;
            }

            ClearCount();
            return false;
        }

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            var digit = key[0] - '0';
            if (digit == 0 && !_hasCount)
            {
                MoveColumnZero();
                return true;
            }
            AppendCount(digit);
            return true;
        }

        if (key == "g")
        {
            _pendingPrefix = "g";
            return true;
        }

        if (key is "f" or "F" or "t" or "T")
        {
            var count = TakeCount(out _);
            _pendingFind = new PendingFind(
                Direction: key is "f" or "t" ? 1 : -1,
                Till: key is "t" or "T",
                Count: count);
            return true;
        }

        var motionCount = TakeCount(out var explicitMotionCount);
        switch (key)
        {
            case "h":
                MoveHorizontal(-motionCount);
                return true;
            case "l":
                MoveHorizontal(motionCount);
                return true;
            case "j":
                MoveVertical(1, motionCount);
                return true;
            case "k":
                MoveVertical(-1, motionCount);
                return true;
            case "w":
                Repeat(motionCount, () => MoveNextWord(bigWord: false));
                return true;
            case "W":
                Repeat(motionCount, () => MoveNextWord(bigWord: true));
                return true;
            case "b":
                Repeat(motionCount, () => MovePreviousWord(bigWord: false));
                return true;
            case "B":
                Repeat(motionCount, () => MovePreviousWord(bigWord: true));
                return true;
            case "e":
                Repeat(motionCount, () => MoveEndWord(bigWord: false));
                return true;
            case "E":
                Repeat(motionCount, () => MoveEndWord(bigWord: true));
                return true;
            case "^":
                MoveFirstNonWhitespace(0);
                return true;
            case "$":
                MoveLineEnd(motionCount - 1);
                return true;
            case "G":
                if (explicitMotionCount) MoveToLine(motionCount);
                else MoveLastLine();
                return true;
            case "%":
                if (explicitMotionCount) MoveToPercentage(motionCount);
                else MoveToMatchingPair();
                return true;
            case ";":
                RepeatLastFind(reverse: false, motionCount);
                return true;
            case ",":
                RepeatLastFind(reverse: true, motionCount);
                return true;
            case "(":
                Repeat(motionCount, () => MoveSentence(-1));
                return true;
            case ")":
                Repeat(motionCount, () => MoveSentence(1));
                return true;
            case "{":
                Repeat(motionCount, () => MoveParagraph(-1));
                return true;
            case "}":
                Repeat(motionCount, () => MoveParagraph(1));
                return true;
            case "+":
            case "Enter":
                MoveLineFirstNonBlank(1, motionCount);
                return true;
            case "-":
                MoveLineFirstNonBlank(-1, motionCount);
                return true;
            case "_":
                MoveFirstNonWhitespace(motionCount - 1);
                return true;
            case "|":
                MoveToColumn(motionCount);
                return true;
            case "H":
                _editor.MoveToViewport(ViViewportTarget.Top, motionCount);
                return true;
            case "M":
                _editor.MoveToViewport(ViViewportTarget.Middle, 1);
                return true;
            case "L":
                _editor.MoveToViewport(ViViewportTarget.Bottom, motionCount);
                return true;
            case "Ctrl+f":
                Repeat(motionCount, () => _editor.ScrollPage(1));
                return true;
            case "Ctrl+b":
                Repeat(motionCount, () => _editor.ScrollPage(-1));
                return true;
            case "Ctrl+d":
                Repeat(motionCount, () => _editor.ScrollHalfPage(1));
                return true;
            case "Ctrl+u":
                Repeat(motionCount, () => _editor.ScrollHalfPage(-1));
                return true;
            case "Ctrl+e":
                Repeat(motionCount, () => _editor.ScrollView(1));
                return true;
            case "Ctrl+y":
                Repeat(motionCount, () => _editor.ScrollView(-1));
                return true;
            default:
                return false;
        }
    }

    public bool HandleCharacter(char target)
    {
        if (_pendingFind is not { } pending) return false;
        _pendingFind = null;
        var motion = new FindMotion(target, pending.Direction, pending.Till);
        ExecuteFind(motion, pending.Count, isRepeat: false);
        _lastFind = motion;
        return true;
    }

    private void AppendCount(int digit)
    {
        _hasCount = true;
        _count = Math.Min(999_999, _count * 10 + digit);
    }

    private int TakeCount(out bool explicitCount)
    {
        explicitCount = _hasCount;
        var value = _hasCount ? Math.Max(1, _count) : 1;
        ClearCount();
        return value;
    }

    private void ClearCount()
    {
        _count = 0;
        _hasCount = false;
    }

    private void ResetPending()
    {
        ClearCount();
        _pendingPrefix = null;
        _pendingFind = null;
    }

    private static void Repeat(int count, Action action)
    {
        for (var i = 0; i < Math.Max(1, count); i++) action();
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

    private void MoveVertical(int direction, int count)
    {
        if (_editor.TextLength == 0) return;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var currentStart = _editor.LineStart(current);
        var desiredColumn = Math.Max(0, current - currentStart);
        var targetStart = currentStart;

        for (var step = 0; step < count; step++)
        {
            var next = direction > 0 ? NextLineStart(targetStart) : PreviousLineStart(targetStart);
            if (next == targetStart) break;
            targetStart = next;
        }

        var targetEnd = _editor.LineEndExclusive(targetStart);
        var maxTarget = targetEnd > targetStart ? targetEnd - 1 : targetStart;
        _editor.MoveCaret(Math.Min(targetStart + desiredColumn, maxTarget));
    }

    private int NextLineStart(int lineStart)
    {
        var length = _editor.TextLength;
        if (length == 0) return 0;
        var end = _editor.LineEndExclusive(lineStart);
        var next = end;
        if (next < length && _editor.CharAt(next) == '\r') next++;
        if (next < length && _editor.CharAt(next) == '\n') next++;
        return next < length ? next : lineStart;
    }

    private int PreviousLineStart(int lineStart)
    {
        if (lineStart <= 0) return 0;
        return _editor.LineStart(lineStart - 1);
    }

    private void MoveColumnZero()
    {
        _editor.MoveCaret(_editor.LineStart(_editor.CaretPosition));
    }

    private void MoveFirstNonWhitespace(int linesDown)
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        for (var i = 0; i < linesDown; i++)
        {
            var next = NextLineStart(start);
            if (next == start) break;
            start = next;
        }
        MoveToFirstNonWhitespace(start);
    }

    private void MoveToFirstNonWhitespace(int lineStart)
    {
        var end = _editor.LineEndExclusive(lineStart);
        var i = lineStart;
        while (i < end && char.IsWhiteSpace(_editor.CharAt(i))) i++;
        _editor.MoveCaret(i < end ? i : lineStart);
    }

    private void MoveLineEnd(int linesDown)
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        for (var i = 0; i < linesDown; i++)
        {
            var next = NextLineStart(start);
            if (next == start) break;
            start = next;
        }
        var end = _editor.LineEndExclusive(start);
        _editor.MoveCaret(end > start ? end - 1 : start);
    }

    private void MoveLineFirstNonBlank(int direction, int count)
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        for (var i = 0; i < count; i++)
        {
            var next = direction > 0 ? NextLineStart(start) : PreviousLineStart(start);
            if (next == start) break;
            start = next;
        }
        MoveToFirstNonWhitespace(start);
    }

    private void MoveToColumn(int oneBasedColumn)
    {
        var start = _editor.LineStart(_editor.CaretPosition);
        var end = _editor.LineEndExclusive(start);
        var max = end > start ? end - 1 : start;
        _editor.MoveCaret(Math.Min(start + Math.Max(0, oneBasedColumn - 1), max));
    }

    private void MoveToLine(int oneBasedLine)
    {
        if (_editor.TextLength == 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        var target = Math.Max(1, oneBasedLine);
        var line = 1;
        var start = 0;
        while (line < target)
        {
            var next = NextLineStart(start);
            if (next == start) break;
            start = next;
            line++;
        }
        MoveToFirstNonWhitespace(start);
    }

    private void MoveLastLine()
    {
        if (_editor.TextLength == 0)
        {
            _editor.MoveCaret(0);
            return;
        }
        var start = _editor.LineStart(_editor.TextLength - 1);
        MoveToFirstNonWhitespace(start);
    }

    private void MoveToPercentage(int percentage)
    {
        if (_editor.TextLength == 0) return;
        percentage = Math.Clamp(percentage, 1, 100);
        var lines = 1;
        for (var i = 0; i < _editor.TextLength; i++)
        {
            if (_editor.CharAt(i) == '\n') lines++;
        }
        var targetLine = (percentage * lines + 99) / 100;
        MoveToLine(targetLine);
    }

    private void MoveNextWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0) return;
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

        if (i < text.Length) _editor.MoveCaret(i);
    }

    private void MovePreviousWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0) return;
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

    private void MoveEndWord(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0) return;
        var i = Math.Clamp(_editor.CaretPosition + 1, 0, text.Length - 1);
        while (i < text.Length - 1 && char.IsWhiteSpace(text[i])) i++;
        if (bigWord)
        {
            while (i < text.Length - 1 && !char.IsWhiteSpace(text[i + 1])) i++;
        }
        else
        {
            var wordClass = ClassifySmallWord(text[i]);
            while (i < text.Length - 1 && ClassifySmallWord(text[i + 1]) == wordClass) i++;
        }
        _editor.MoveCaret(i);
    }

    private void MovePreviousWordEnd(bool bigWord)
    {
        var text = _editor.Text;
        if (text.Length == 0) return;
        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        var i = current - 1;
        if (i < 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        if (!char.IsWhiteSpace(text[current]) && !char.IsWhiteSpace(text[i]))
        {
            var currentClass = ClassifySmallWord(text[current]);
            while (i >= 0)
            {
                var sameCurrent = bigWord
                    ? !char.IsWhiteSpace(text[i])
                    : ClassifySmallWord(text[i]) == currentClass;
                if (!sameCurrent) break;
                i--;
            }
        }

        while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
        if (i < 0)
        {
            _editor.MoveCaret(0);
            return;
        }

        _editor.MoveCaret(i);
    }

    private void MoveToMatchingPair()
    {
        if (_editor.TextLength == 0) return;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var lineEnd = _editor.LineEndExclusive(current);
        var position = current;
        while (position < lineEnd && !IsPairCharacter(_editor.CharAt(position))) position++;
        if (position >= lineEnd) return;

        var currentChar = _editor.CharAt(position);
        var (open, close, direction) = currentChar switch
        {
            '(' => ('(', ')', 1),
            '[' => ('[', ']', 1),
            '{' => ('{', '}', 1),
            ')' => ('(', ')', -1),
            ']' => ('[', ']', -1),
            '}' => ('{', '}', -1),
            _ => ('\0', '\0', 0)
        };
        if (direction == 0) return;

        var depth = 0;
        for (var i = position; i >= 0 && i < _editor.TextLength; i += direction)
        {
            var c = _editor.CharAt(i);
            if (direction > 0)
            {
                if (c == open) depth++;
                else if (c == close && --depth == 0)
                {
                    _editor.MoveCaret(i);
                    return;
                }
            }
            else
            {
                if (c == close) depth++;
                else if (c == open && --depth == 0)
                {
                    _editor.MoveCaret(i);
                    return;
                }
            }
        }
    }

    private static bool IsPairCharacter(char c) => c is '(' or ')' or '[' or ']' or '{' or '}';

    private void RepeatLastFind(bool reverse, int count)
    {
        if (_lastFind is not { } last) return;
        var motion = reverse ? last with { Direction = -last.Direction } : last;
        for (var i = 0; i < count; i++)
        {
            if (!ExecuteFind(motion, 1, isRepeat: true)) break;
        }
    }

    private bool ExecuteFind(FindMotion motion, int occurrenceCount, bool isRepeat)
    {
        if (_editor.TextLength == 0) return false;
        var current = Math.Clamp(_editor.CaretPosition, 0, _editor.TextLength - 1);
        var lineStart = _editor.LineStart(current);
        var lineEnd = _editor.LineEndExclusive(current);
        var position = current + motion.Direction;
        if (motion.Till && isRepeat) position += motion.Direction;

        var found = -1;
        for (var occurrence = 0; occurrence < Math.Max(1, occurrenceCount); occurrence++)
        {
            found = -1;
            if (motion.Direction > 0)
            {
                for (var i = Math.Max(lineStart, position); i < lineEnd; i++)
                {
                    if (_editor.CharAt(i) != motion.Target) continue;
                    found = i;
                    position = i + 1;
                    break;
                }
            }
            else
            {
                for (var i = Math.Min(lineEnd - 1, position); i >= lineStart; i--)
                {
                    if (_editor.CharAt(i) != motion.Target) continue;
                    found = i;
                    position = i - 1;
                    break;
                }
            }
            if (found < 0) return false;
        }

        var target = motion.Till ? found - motion.Direction : found;
        target = Math.Clamp(target, lineStart, Math.Max(lineStart, lineEnd - 1));
        _editor.MoveCaret(target);
        return true;
    }

    private void MoveSentence(int direction)
    {
        var text = _editor.Text;
        if (text.Length == 0) return;
        var starts = BuildSentenceStarts(text);
        var current = Math.Clamp(_editor.CaretPosition, 0, text.Length - 1);
        if (direction > 0)
        {
            var target = starts.FirstOrDefault(p => p > current, -1);
            if (target >= 0) _editor.MoveCaret(target);
        }
        else
        {
            for (var i = starts.Count - 1; i >= 0; i--)
            {
                if (starts[i] >= current) continue;
                _editor.MoveCaret(starts[i]);
                return;
            }
        }
    }

    private static List<int> BuildSentenceStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var boundary = c is '.' or '!' or '?';
            if (boundary)
            {
                var j = i + 1;
                while (j < text.Length && text[j] is ')' or ']' or '}' or '"' or '\'') j++;
                if (j >= text.Length || char.IsWhiteSpace(text[j]))
                {
                    while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                    if (j < text.Length && starts[^1] != j) starts.Add(j);
                }
            }

            if (c == '\n' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                var j = i + 2;
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                if (j < text.Length && starts[^1] != j) starts.Add(j);
            }
        }
        return starts;
    }

    private void MoveParagraph(int direction)
    {
        if (_editor.TextLength == 0) return;
        var currentStart = _editor.LineStart(_editor.CaretPosition);
        if (direction > 0)
        {
            var start = currentStart;
            var sawEmpty = IsEmptyLine(start);
            while (true)
            {
                var next = NextLineStart(start);
                if (next == start) return;
                start = next;
                if (IsEmptyLine(start))
                {
                    sawEmpty = true;
                    continue;
                }
                if (!sawEmpty) continue;
                MoveToFirstNonWhitespace(start);
                return;
            }
        }
        else
        {
            var start = PreviousLineStart(currentStart);
            if (start == currentStart) return;
            while (start > 0 && IsEmptyLine(start))
            {
                var previous = PreviousLineStart(start);
                if (previous == start) break;
                start = previous;
            }
            while (start > 0)
            {
                var previous = PreviousLineStart(start);
                if (previous == start || IsEmptyLine(previous)) break;
                start = previous;
            }
            MoveToFirstNonWhitespace(start);
        }
    }

    private bool IsEmptyLine(int lineStart) => _editor.LineEndExclusive(lineStart) == lineStart;

    private static SmallWordClass ClassifySmallWord(char c)
    {
        if (char.IsWhiteSpace(c)) return SmallWordClass.Whitespace;
        if (char.IsLetterOrDigit(c) || c == '_') return SmallWordClass.Keyword;
        return SmallWordClass.Punctuation;
    }

    private readonly record struct PendingFind(int Direction, bool Till, int Count);
    private readonly record struct FindMotion(char Target, int Direction, bool Till);

    private enum SmallWordClass
    {
        Whitespace,
        Keyword,
        Punctuation
    }
}
