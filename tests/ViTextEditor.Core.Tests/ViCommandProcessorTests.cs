using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class ViCommandProcessorTests
{
    [Fact]
    public void NumericColonCommandMovesToOneBasedLine()
    {
        var editor = new FakeEditor("one\ntwo\nthree\nfour", 0);
        var commands = new ViCommandProcessor(editor);

        Assert.True(commands.Execute("3"));
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void DollarColonCommandMovesToLastLine()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var commands = new ViCommandProcessor(editor);

        Assert.True(commands.Execute("$"));
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void OversizedLineNumberClampsToLastLine()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var commands = new ViCommandProcessor(editor);

        Assert.True(commands.Execute("999"));
        Assert.Equal(8, editor.CaretPosition);
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
