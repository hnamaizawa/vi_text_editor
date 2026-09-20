using ScintillaNET;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal sealed class ScintillaEditorAdapter : IEditorAdapter
{
    private readonly Scintilla _editor;

    public ScintillaEditorAdapter(Scintilla editor)
    {
        _editor = editor;
    }

    public string Text => _editor.Text;
    public int CaretPosition => _editor.CurrentPosition;
    public int TextLength => _editor.TextLength;

    public char CharAt(int position)
    {
        if (position < 0 || position >= _editor.TextLength)
        {
            return '\0';
        }
        return (char)_editor.GetCharAt(position);
    }

    public int LineStart(int position)
    {
        if (_editor.TextLength == 0)
        {
            return 0;
        }
        var line = _editor.LineFromPosition(Math.Clamp(position, 0, _editor.TextLength));
        return _editor.Lines[Math.Clamp(line, 0, _editor.Lines.Count - 1)].Position;
    }

    public int LineEndExclusive(int position)
    {
        if (_editor.TextLength == 0)
        {
            return 0;
        }

        var line = _editor.LineFromPosition(Math.Clamp(position, 0, _editor.TextLength));
        var item = _editor.Lines[Math.Clamp(line, 0, _editor.Lines.Count - 1)];
        var end = item.EndPosition;
        // Line.EndPosition includes CR/LF because Line.Length includes EOL characters.
        if (end > item.Position && CharAt(end - 1) == '\n') end--;
        if (end > item.Position && CharAt(end - 1) == '\r') end--;
        return end;
    }

    public string GetTextRange(int position, int length)
    {
        position = Math.Clamp(position, 0, _editor.TextLength);
        length = Math.Clamp(length, 0, _editor.TextLength - position);
        return _editor.GetTextRange(position, length);
    }

    public void MoveCaret(int position)
    {
        _editor.GotoPosition(Math.Clamp(position, 0, _editor.TextLength));
    }

    public void DeleteRange(int position, int length)
    {
        _editor.DeleteRange(position, length);
    }

    public void InsertText(int position, string text)
    {
        _editor.GotoPosition(Math.Clamp(position, 0, _editor.TextLength));
        _editor.AddText(text);
    }

    public void Undo() => _editor.Undo();
    public void Redo() => _editor.Redo();

    public void ScrollPage(int direction)
    {
        if (direction == 0) return;
        ScrollByVisibleLines(Math.Sign(direction) * Math.Max(1, _editor.LinesOnScreen - 2));
    }

    public void ScrollHalfPage(int direction)
    {
        if (direction == 0) return;
        ScrollByVisibleLines(Math.Sign(direction) * Math.Max(1, _editor.LinesOnScreen / 2));
    }

    private void ScrollByVisibleLines(int deltaLines)
    {
        if (_editor.Lines.Count == 0 || deltaLines == 0) return;

        var currentLine = Math.Clamp(_editor.CurrentLine, 0, _editor.Lines.Count - 1);
        var currentLineStart = _editor.Lines[currentLine].Position;
        var column = Math.Max(0, _editor.CurrentPosition - currentLineStart);
        var targetLine = Math.Clamp(currentLine + deltaLines, 0, _editor.Lines.Count - 1);
        var target = _editor.Lines[targetLine];
        var targetEnd = LineEndExclusive(target.Position);
        var maxTargetPosition = Math.Max(target.Position, targetEnd > target.Position ? targetEnd - 1 : target.Position);
        var targetPosition = Math.Min(target.Position + column, maxTargetPosition);

        var targetFirstVisibleLine = Math.Clamp(
            _editor.FirstVisibleLine + deltaLines,
            0,
            Math.Max(0, _editor.Lines.Count - 1));

        _editor.FirstVisibleLine = targetFirstVisibleLine;
        _editor.GotoPosition(targetPosition);
    }
}
