using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class V017AdditionalMotionTests
{
    [Fact]
    public void VerticalMotionDoesNotSkipBlankLines()
    {
        var editor = new FakeEditor("one\n\nthree", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("j");
        Assert.Equal(4, editor.CaretPosition);
        navigation.Handle("j");
        Assert.Equal(5, editor.CaretPosition);
        navigation.Handle("k");
        Assert.Equal(4, editor.CaretPosition);
    }

    [Fact]
    public void GeMovesBackwardToEndOfPreviousWord()
    {
        var editor = new FakeEditor("one two three", 8);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("g");
        navigation.Handle("e");

        Assert.Equal(6, editor.CaretPosition);
    }

    [Fact]
    public void CountedGeRepeatsBackwardWordEndMotion()
    {
        var editor = new FakeEditor("one two three four", 14);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("2");
        navigation.Handle("g");
        navigation.Handle("e");

        Assert.Equal(6, editor.CaretPosition);
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
        public void Undo() { }
        public void Redo() { }
    }
}
