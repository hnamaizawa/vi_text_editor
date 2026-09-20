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

    [Fact]
    public void YankRangeIntoNamedRegisterAndPutAfterCurrentLine()
    {
        var editor = new FakeEditor("one\ntwo\nthree\nfour", 0);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);

        Assert.True(commands.Execute("2,3y a"));
        Assert.Equal("one\ntwo\nthree\nfour", editor.Text);

        editor.MoveCaret(0);
        Assert.True(commands.Execute("pu a"));
        Assert.Equal("one\ntwo\nthree\ntwo\nthree\nfour", editor.Text);
    }

    [Fact]
    public void YankAbbreviationsAreAccepted()
    {
        foreach (var command in new[] { "2y a", "2ya a", "2yan a", "2yank a" })
        {
            var editor = new FakeEditor("one\ntwo\nthree", 0);
            var registers = new ViRegisterStore();
            var commands = new ViCommandProcessor(editor, registers);

            Assert.True(commands.Execute(command));
            Assert.True(registers.TryGet('a', out var lines));
            Assert.Equal(new[] { "two" }, lines);
        }
    }

    [Fact]
    public void CompactYankCountYanksFromCurrentLine()
    {
        var editor = new FakeEditor("one\ntwo\nthree\nfour\nfive", 4);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);

        Assert.True(commands.Execute("y3"));
        Assert.True(registers.TryGet(null, out var lines));
        Assert.Equal(new[] { "two", "three", "four" }, lines);
    }

    [Fact]
    public void YankCountAlsoAcceptsVimStyleSpacingAndNamedRegister()
    {
        foreach (var command in new[] { "y 3", "yank 3", "y a 3" })
        {
            var editor = new FakeEditor("one\ntwo\nthree\nfour", 4);
            var registers = new ViRegisterStore();
            var commands = new ViCommandProcessor(editor, registers);

            Assert.True(commands.Execute(command));
            var register = command == "y a 3" ? 'a' : (char?)null;
            Assert.True(registers.TryGet(register, out var lines));
            Assert.Equal(new[] { "two", "three", "four" }, lines);
        }
    }

    [Fact]
    public void YankCountClampsAtEndOfFileAndRejectsZero()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 4);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);

        Assert.True(commands.Execute("y9"));
        Assert.True(registers.TryGet(null, out var lines));
        Assert.Equal(new[] { "two", "three" }, lines);
        Assert.False(commands.Execute("y0"));
    }

    [Fact]
    public void ExYankCanBePastedWithNormalModeP()
    {
        var editor = new FakeEditor("one\ntwo\nthree\nfour", 4);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);
        var vi = new ViKeyProcessor(editor, registers);

        Assert.True(commands.Execute("y2"));
        editor.MoveCaret(0);
        Assert.True(vi.Handle("p"));

        Assert.Equal("one\ntwo\nthree\ntwo\nthree\nfour", editor.Text);
    }

    [Fact]
    public void NormalModeYankCanBePutWithExCommand()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);
        var vi = new ViKeyProcessor(editor, registers);

        Assert.True(vi.Handle("y"));
        Assert.True(vi.Handle("y"));
        Assert.True(commands.Execute("2pu"));

        Assert.Equal("one\ntwo\none\nthree", editor.Text);
    }

    [Fact]
    public void PutCanTargetSpecificLineOrZero()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var registers = new ViRegisterStore();
        registers.Yank('a', new[] { "X" });
        var commands = new ViCommandProcessor(editor, registers);

        Assert.True(commands.Execute("2pu a"));
        Assert.Equal("one\ntwo\nX\nthree", editor.Text);

        Assert.True(commands.Execute("0put a"));
        Assert.Equal("X\none\ntwo\nX\nthree", editor.Text);
    }

    [Fact]
    public void UppercaseNamedRegisterAppendsLikeVim()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var registers = new ViRegisterStore();
        var commands = new ViCommandProcessor(editor, registers);

        Assert.True(commands.Execute("1y a"));
        Assert.True(commands.Execute("2y A"));
        Assert.True(registers.TryGet('a', out var lines));
        Assert.Equal(new[] { "one", "two" }, lines);
    }

    [Fact]
    public void PutIsReportedAsMutatingButYankAndJumpAreNot()
    {
        var editor = new FakeEditor("one\ntwo", 0);
        var commands = new ViCommandProcessor(editor);

        Assert.True(commands.IsMutatingCommand("pu a"));
        Assert.True(commands.IsMutatingCommand("20put a"));
        Assert.False(commands.IsMutatingCommand("2y a"));
        Assert.False(commands.IsMutatingCommand("y3"));
        Assert.False(commands.IsMutatingCommand("2"));
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
        public void InsertText(int position, string text)
        {
            Text = Text.Insert(position, text);
            CaretPosition = position + text.Length;
        }
        public void Undo() { }
        public void Redo() { }
    }
}
