using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class ViSearchProcessorTests
{
    [Fact]
    public void ForwardSearchThenNRepeatsInSameDirection()
    {
        var editor = new FakeEditor("alpha beta alpha beta", 0);
        var search = new ViSearchProcessor(editor);

        Assert.True(search.Search("beta", forward: true));
        Assert.Equal(6, editor.CaretPosition);

        Assert.True(search.Repeat(reverseDirection: false));
        Assert.Equal(17, editor.CaretPosition);
    }

    [Fact]
    public void UppercaseNRepeatsInOppositeDirection()
    {
        var editor = new FakeEditor("alpha beta alpha beta", 17);
        var search = new ViSearchProcessor(editor);

        Assert.True(search.Search("beta", forward: false));
        Assert.Equal(6, editor.CaretPosition);

        Assert.True(search.Repeat(reverseDirection: true));
        Assert.Equal(17, editor.CaretPosition);
    }

    [Fact]
    public void SearchWrapsAroundLikeVimWrapscan()
    {
        var editor = new FakeEditor("one two one", 8);
        var search = new ViSearchProcessor(editor);

        Assert.True(search.Search("one", forward: true));
        Assert.Equal(0, editor.CaretPosition);
    }

    private sealed class FakeEditor : IEditorAdapter
    {
        public FakeEditor(string text, int caret)
        {
            Text = text;
            CaretPosition = caret;
        }

        public string Text { get; private set; }
        public int CaretPosition { get; private set; }
        public void MoveCaret(int position) => CaretPosition = Math.Clamp(position, 0, Text.Length);
        public void DeleteRange(int position, int length) => Text = Text.Remove(position, length);
        public void InsertText(int position, string text) => Text = Text.Insert(position, text);
        public void Undo() { }
        public void Redo() { }
    }
}
