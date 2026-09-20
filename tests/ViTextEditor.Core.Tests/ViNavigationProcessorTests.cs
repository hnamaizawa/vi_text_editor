using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class ViNavigationProcessorTests
{
    [Fact]
    public void LowercaseWMovesByVimWordIncludingPunctuationRuns()
    {
        var (editor, navigation) = Create("foo-bar baz");

        Assert.True(navigation.Handle("w"));
        Assert.Equal(3, editor.CaretPosition);

        navigation.Handle("w");
        Assert.Equal(4, editor.CaretPosition);

        navigation.Handle("w");
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void UppercaseWMovesByWhitespaceSeparatedWord()
    {
        var (editor, navigation) = Create("foo-bar baz");

        Assert.True(navigation.Handle("W"));
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void LowercaseBMovesBackByVimWordIncludingPunctuationRuns()
    {
        var (editor, navigation) = Create("foo-bar baz", 8);

        Assert.True(navigation.Handle("b"));
        Assert.Equal(4, editor.CaretPosition);

        navigation.Handle("b");
        Assert.Equal(3, editor.CaretPosition);

        navigation.Handle("b");
        Assert.Equal(0, editor.CaretPosition);
    }

    [Fact]
    public void UppercaseBMovesBackByWhitespaceSeparatedWord()
    {
        var (editor, navigation) = Create("foo-bar baz", 10);

        Assert.True(navigation.Handle("B"));
        Assert.Equal(8, editor.CaretPosition);

        navigation.Handle("B");
        Assert.Equal(0, editor.CaretPosition);
    }

    [Fact]
    public void CtrlFAndCtrlBDelegateFullPageScrolling()
    {
        var (editor, navigation) = Create("one\ntwo\nthree");

        Assert.True(navigation.Handle("Ctrl+f"));
        Assert.True(navigation.Handle("Ctrl+b"));

        Assert.Equal(new[] { 1, -1 }, editor.FullPageScrollDirections);
    }

    [Fact]
    public void CtrlDAndCtrlUDelegateHalfPageScrolling()
    {
        var (editor, navigation) = Create("one\ntwo\nthree");

        Assert.True(navigation.Handle("Ctrl+d"));
        Assert.True(navigation.Handle("Ctrl+u"));

        Assert.Equal(new[] { 1, -1 }, editor.HalfPageScrollDirections);
    }

    private static (FakeEditor Editor, ViNavigationProcessor Navigation) Create(string text, int caret = 0)
    {
        var editor = new FakeEditor(text, caret);
        return (editor, new ViNavigationProcessor(editor));
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
        public List<int> FullPageScrollDirections { get; } = [];
        public List<int> HalfPageScrollDirections { get; } = [];

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

        public void Undo()
        {
        }

        public void Redo()
        {
        }

        public void ScrollPage(int direction) => FullPageScrollDirections.Add(direction);
        public void ScrollHalfPage(int direction) => HalfPageScrollDirections.Add(direction);
    }
}
