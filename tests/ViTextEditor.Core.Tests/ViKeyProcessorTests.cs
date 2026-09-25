using ViTextEditor.Core.Editor;
using Xunit;

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
    public void CaretMovesToFirstNonBlankCharacterOfCurrentLine()
    {
        var (editor, vi) = Create("one\n    two\nthree", 10);
        Assert.True(vi.Handle("^"));
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void CwChangesCurrentWordAndEntersInsertMode()
    {
        var (editor, vi) = Create("alpha beta", 0);
        vi.Handle("c");
        Assert.True(vi.Handle("w"));
        Assert.Equal(" beta", editor.Text);
        Assert.Equal(0, editor.CaretPosition);
        Assert.Equal(EditorMode.Insert, vi.Mode);
    }

    [Fact]
    public void CwFromMiddleOfWordChangesFromCaretToWordEnd()
    {
        var (editor, vi) = Create("alpha beta", 2);
        vi.Handle("c");
        vi.Handle("w");
        Assert.Equal("al beta", editor.Text);
        Assert.Equal(2, editor.CaretPosition);
        Assert.Equal(EditorMode.Insert, vi.Mode);
    }

    [Fact]
    public void UppercaseCwChangesWhitespaceSeparatedWord()
    {
        var (editor, vi) = Create("foo-bar baz", 0);
        vi.Handle("c");
        vi.Handle("W");
        Assert.Equal(" baz", editor.Text);
        Assert.Equal(EditorMode.Insert, vi.Mode);
    }

    [Fact]
    public void CeChangesCurrentWordAndEntersInsertMode()
    {
        var (editor, vi) = Create("alpha beta", 0);
        vi.Handle("c");
        vi.Handle("e");
        Assert.Equal(" beta", editor.Text);
        Assert.Equal(EditorMode.Insert, vi.Mode);
    }

    [Fact]
    public void CDollarChangesToEndOfLine()
    {
        var (editor, vi) = Create("one two\nthree", 4);
        vi.Handle("c");
        vi.Handle("$");
        Assert.Equal("one \nthree", editor.Text);
        Assert.Equal(EditorMode.Insert, vi.Mode);
    }

    [Fact]
    public void DwDeletesWordAndFollowingSpaceWithoutEnteringInsertMode()
    {
        var (editor, vi) = Create("alpha beta", 0);
        vi.Handle("d");
        vi.Handle("w");
        Assert.Equal("beta", editor.Text);
        Assert.Equal(0, editor.CaretPosition);
        Assert.Equal(EditorMode.Normal, vi.Mode);
    }

    [Fact]
    public void DWDeletesWhitespaceSeparatedWordAndFollowingSpace()
    {
        var (editor, vi) = Create("foo-bar baz", 0);
        vi.Handle("d");
        vi.Handle("W");
        Assert.Equal("baz", editor.Text);
        Assert.Equal(EditorMode.Normal, vi.Mode);
    }

    [Fact]
    public void DwDoesNotDeleteLineBreakAtEndOfLine()
    {
        var (editor, vi) = Create("alpha\nbeta", 0);
        vi.Handle("d");
        vi.Handle("w");
        Assert.Equal("\nbeta", editor.Text);
    }

    [Fact]
    public void DeAndDEDeleteToWordEnd()
    {
        var (smallEditor, smallVi) = Create("alpha beta", 2);
        smallVi.Handle("d");
        smallVi.Handle("e");
        Assert.Equal("al beta", smallEditor.Text);

        var (bigEditor, bigVi) = Create("foo-bar baz", 0);
        bigVi.Handle("d");
        bigVi.Handle("E");
        Assert.Equal(" baz", bigEditor.Text);
    }

    [Fact]
    public void DAndDDollarDeleteToEndOfLine()
    {
        var (editor1, vi1) = Create("one two\nthree", 4);
        vi1.Handle("D");
        Assert.Equal("one \nthree", editor1.Text);

        var (editor2, vi2) = Create("one two\nthree", 4);
        vi2.Handle("d");
        vi2.Handle("$");
        Assert.Equal("one \nthree", editor2.Text);
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
    public void DCountDDeletesAndYanksRequestedLinesForPut()
    {
        var (editor, vi) = Create("one\ntwo\nthree\nfour\nfive", 4);

        vi.Handle("d");
        vi.Handle("3");
        vi.Handle("d");

        Assert.Equal("one\nfive", editor.Text);
        Assert.Equal(EditorMode.Normal, vi.Mode);

        vi.Handle("p");
        Assert.Equal("one\nfive\ntwo\nthree\nfour", editor.Text);
    }

    [Fact]
    public void DCountDClampsAtEndOfFileAndDotRepeatsCount()
    {
        var (editor, vi) = Create("one\ntwo\nthree\nfour\nfive\nsix", 0);
        vi.Handle("d");
        vi.Handle("2");
        vi.Handle("d");
        vi.Handle(".");
        Assert.Equal("five\nsix", editor.Text);

        vi.Handle("d");
        vi.Handle("9");
        vi.Handle("d");
        Assert.Equal(string.Empty, editor.Text);
    }

    [Fact]
    public void CwReplacesWholeWordAfterTypingAndEscape()
    {
        var (editor, vi) = Create("abc tail", 0);
        vi.Handle("c");
        vi.Handle("w");
        editor.InsertText(editor.CaretPosition, "xy");
        vi.Handle("Esc");

        Assert.Equal("xy tail", editor.Text);
        Assert.Equal(EditorMode.Normal, vi.Mode);
    }

    [Fact]
    public void CcAndUppercaseCEnterInsertModeAfterDeletingTarget()
    {
        var (lineEditor, lineVi) = Create("one\ntwo\nthree", 4);
        lineVi.Handle("c");
        lineVi.Handle("c");
        Assert.Equal("one\n\nthree", lineEditor.Text);
        Assert.Equal(4, lineEditor.CaretPosition);
        Assert.Equal(EditorMode.Insert, lineVi.Mode);

        var (tailEditor, tailVi) = Create("one two\nthree", 4);
        tailVi.Handle("C");
        Assert.Equal("one \nthree", tailEditor.Text);
        Assert.Equal(EditorMode.Insert, tailVi.Mode);
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
    public void DotRepeatsLastPutButNotTheYank()
    {
        var (editor, vi) = Create("one\ntwo", 0);
        vi.Handle("y");
        vi.Handle("y");
        vi.Handle("p");
        vi.Handle(".");
        Assert.Equal("one\none\none\ntwo", editor.Text);
    }

    [Fact]
    public void DotRepeatsCharacterDelete()
    {
        var (editor, vi) = Create("abc", 0);
        vi.Handle("x");
        vi.Handle(".");
        Assert.Equal("c", editor.Text);
    }

    [Fact]
    public void DotRepeatsDeleteWordMotion()
    {
        var (editor, vi) = Create("one two three", 0);
        vi.Handle("d");
        vi.Handle("w");
        vi.Handle(".");
        Assert.Equal("three", editor.Text);
    }

    [Fact]
    public void DotRepeatsChangeIncludingInsertedText()
    {
        var (editor, vi) = Create("one two", 0);
        vi.Handle("c");
        vi.Handle("w");
        editor.InsertText(editor.CaretPosition, "X");
        vi.Handle("Esc");
        vi.CommitInsertRepeat([ViRepeatEdit.Insert(0, "X")]);

        editor.MoveCaret(2);
        vi.Handle(".");

        Assert.Equal("X X", editor.Text);
        Assert.Equal(EditorMode.Normal, vi.Mode);
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
