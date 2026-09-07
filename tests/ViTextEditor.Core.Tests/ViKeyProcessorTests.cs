using ViTextEditor.Core.Editor;

namespace ViTextEditor.Core.Tests;

public sealed class ViKeyProcessorTests
{
    [Fact]
    public void StartsInNormalMode()
    {
        var (_, vi) = Create("abc");
        Assert.Equal(EditorMode.Normal, vi.Mode);
    }

    [Fact]
    public void InsertModeAllowsNormalTypingToPassThrough()
    {
        var (_, vi) = Create("abc");
        Assert.True(vi.Handle("i"));
        Assert.Equal(EditorMode.Insert, vi.Mode);
        Assert.False(vi.Handle("日本語IME入力"));
    }

    [Fact]
    public void EscapeReturnsToNormalMode()
    {
        var (editor, vi) = Create("abc", 3);
        vi.Handle("i");
        Assert.True(vi.Handle("Esc"));
        Assert.Equal(EditorMode.Normal, vi.Mode);
        Assert.Equal(2, editor.CaretPosition);
    }

    [Fact]
    public void GgMovesToBeginning()
    {
        var (editor, vi) = Create("one\ntwo\nthree", 8);
        vi.Handle("g");
        vi.Handle("g");
        Assert.Equal(0, editor.CaretPosition);
    }

    [Fact]
    public void DdDeletesCurrentLine()
    {
        var (editor, vi) = Create("one\ntwo\nthree", 5);
        vi.Handle("d");
        vi.Handle("d");
        Assert.Equal("one\nthree", editor.Text);
    }

    [Fact]
    public void YyThenPPastesLineBelow()
    {
        var (editor, vi) = Create("one\ntwo", 0);
        vi.Handle("y");
        vi.Handle("y");
        vi.Handle("p");
        Assert.Equal("one\none\ntwo", editor.Text);
    }

    [Fact]
    public void XDeletesCharacterAndUppercasePCanRestoreItBeforeCaret()
    {
        var (editor, vi) = Create("abc", 1);
        vi.Handle("x");
        Assert.Equal("ac", editor.Text);
        vi.Handle("P");
        Assert.Equal("abc", editor.Text);
    }

    [Fact]
    public void JAndKPreserveColumnWherePossible()
    {
        var (editor, vi) = Create("abcd\nxy\n12345", 3);
        vi.Handle("j");
        Assert.Equal(6, editor.CaretPosition);
        vi.Handle("j");
        Assert.Equal(9, editor.CaretPosition);
        vi.Handle("k");
        Assert.Equal(6, editor.CaretPosition);
    }

    [Fact]
    public void UndoAndRedoAreDelegated()
    {
        var (editor, vi) = Create("abc");
        vi.Handle("u");
        vi.Handle("Ctrl+r");
        Assert.Equal(1, editor.UndoCalls);
        Assert.Equal(1, editor.RedoCalls);
    }

    private static (FakeEditor Editor, ViKeyProcessor Vi) Create(string text, int caret = 0)
    {
        var editor = new FakeEditor(text, caret);
        return (editor, new ViKeyProcessor(editor));
    }

    private sealed class FakeEditor : IEditorAdapter
    {
        private string _text;
        public FakeEditor(string text, int caret)
        {
            _text = text;
            CaretPosition = Math.Clamp(caret, 0, text.Length);
        }

        public string Text => _text;
        public int CaretPosition { get; private set; }
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }

        public void MoveCaret(int position) => CaretPosition = Math.Clamp(position, 0, _text.Length);

        public void DeleteRange(int position, int length)
        {
            position = Math.Clamp(position, 0, _text.Length);
            length = Math.Clamp(length, 0, _text.Length - position);
            _text = _text.Remove(position, length);
            CaretPosition = Math.Min(CaretPosition, _text.Length);
        }

        public void InsertText(int position, string text)
        {
            position = Math.Clamp(position, 0, _text.Length);
            _text = _text.Insert(position, text);
            CaretPosition = position + text.Length;
        }

        public void Undo() => UndoCalls++;
        public void Redo() => RedoCalls++;
    }
}
