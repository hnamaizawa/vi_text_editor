using System.Text.Json;
using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class V017JsonAndMotionTests
{
    [Fact]
    public void CompactJsonWithoutLineBreaksFormatsSuccessfully()
    {
        Assert.True(JsonFormattingService.TryFormat("{\"a\":1,\"b\":[true,false],\"c\":{\"d\":2}}", "\r\n", out var formatted, out var error));
        Assert.Null(error);
        Assert.Contains("\r\n", formatted);
        using var parsed = JsonDocument.Parse(formatted);
        Assert.Equal(1, parsed.RootElement.GetProperty("a").GetInt32());
        Assert.Equal(2, parsed.RootElement.GetProperty("c").GetProperty("d").GetInt32());
    }

    [Fact]
    public void JsonFormatterNormalizesBomUnicodeWhitespaceAndSeparatedNumericSigns()
    {
        var input = "\uFEFF{\"negative\":-\u00A0 12,\"positive\":+\u2009 3}";

        Assert.True(JsonFormattingService.TryFormat(input, "\n", out var formatted, out var error));
        Assert.Null(error);
        using var parsed = JsonDocument.Parse(formatted);
        Assert.Equal(-12, parsed.RootElement.GetProperty("negative").GetInt32());
        Assert.Equal(3, parsed.RootElement.GetProperty("positive").GetInt32());
    }

    [Fact]
    public void JsonFormatterDoesNotNormalizeInsideQuotedStrings()
    {
        const string value = "- 12 + 3\u00A0";
        var input = $"{{\"text\":{JsonSerializer.Serialize(value)},\"n\":- 7}}";

        Assert.True(JsonFormattingService.TryFormat(input, "\n", out var formatted, out var error));
        Assert.Null(error);
        using var parsed = JsonDocument.Parse(formatted);
        Assert.Equal(value, parsed.RootElement.GetProperty("text").GetString());
        Assert.Equal(-7, parsed.RootElement.GetProperty("n").GetInt32());
    }

    [Fact]
    public void PercentJumpsBetweenNestedMatchingBraces()
    {
        var editor = new FakeEditor("{[()]}", 0);
        var navigation = new ViNavigationProcessor(editor);

        Assert.True(navigation.Handle("%"));
        Assert.Equal(5, editor.CaretPosition);
        Assert.True(navigation.Handle("%"));
        Assert.Equal(0, editor.CaretPosition);
    }

    [Fact]
    public void PercentFindsNextPairCharacterOnCurrentLine()
    {
        var editor = new FakeEditor("abc { x } tail", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("%");
        Assert.Equal(8, editor.CaretPosition);
    }

    [Fact]
    public void CountPercentJumpsToPercentageOfFile()
    {
        var editor = new FakeEditor("one\ntwo\nthree\nfour\nfive", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("5");
        navigation.Handle("0");
        navigation.Handle("%");

        Assert.Equal(editor.Text.IndexOf("three", StringComparison.Ordinal), editor.CaretPosition);
    }

    [Fact]
    public void FindTillAndRepeatMotionsWorkLikeVim()
    {
        var editor = new FakeEditor("abxcxdx", 0);
        var navigation = new ViNavigationProcessor(editor);

        Assert.True(navigation.Handle("f"));
        Assert.True(navigation.IsAwaitingCharacter);
        Assert.True(navigation.HandleCharacter('x'));
        Assert.Equal(2, editor.CaretPosition);

        navigation.Handle(";");
        Assert.Equal(4, editor.CaretPosition);
        navigation.Handle(",");
        Assert.Equal(2, editor.CaretPosition);

        editor.MoveCaret(0);
        navigation.Handle("t");
        navigation.HandleCharacter('x');
        Assert.Equal(1, editor.CaretPosition);
    }

    [Fact]
    public void CountsApplyToCommonCursorMotions()
    {
        var editor = new FakeEditor("one two three four\na\nb\nc\nd", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("3");
        navigation.Handle("w");
        Assert.Equal(editor.Text.IndexOf("four", StringComparison.Ordinal), editor.CaretPosition);

        editor.MoveCaret(editor.Text.IndexOf("a\n", StringComparison.Ordinal));
        navigation.Handle("3");
        navigation.Handle("j");
        Assert.Equal(editor.Text.LastIndexOf("d", StringComparison.Ordinal), editor.CaretPosition);
    }

    [Fact]
    public void LineColumnAndGotoMotionsUseCounts()
    {
        var editor = new FakeEditor("abcdef\n  second\nthird\nfourth", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("4");
        navigation.Handle("|");
        Assert.Equal(3, editor.CaretPosition);

        navigation.Handle("3");
        navigation.Handle("G");
        Assert.Equal(editor.Text.IndexOf("third", StringComparison.Ordinal), editor.CaretPosition);

        navigation.Handle("2");
        navigation.Handle("g");
        navigation.Handle("g");
        Assert.Equal(editor.Text.IndexOf("second", StringComparison.Ordinal), editor.CaretPosition);
    }

    [Fact]
    public void SentenceAndParagraphMotionsNavigateTextObjects()
    {
        var sentenceEditor = new FakeEditor("One. Two! Three?", 0);
        var sentenceNavigation = new ViNavigationProcessor(sentenceEditor);
        sentenceNavigation.Handle(")");
        Assert.Equal(5, sentenceEditor.CaretPosition);
        sentenceNavigation.Handle(")");
        Assert.Equal(10, sentenceEditor.CaretPosition);
        sentenceNavigation.Handle("(");
        Assert.Equal(5, sentenceEditor.CaretPosition);

        var paragraphText = "one\nline\n\npara2\nline\n\npara3";
        var paragraphEditor = new FakeEditor(paragraphText, 0);
        var paragraphNavigation = new ViNavigationProcessor(paragraphEditor);
        paragraphNavigation.Handle("}");
        Assert.Equal(paragraphText.IndexOf("para2", StringComparison.Ordinal), paragraphEditor.CaretPosition);
        paragraphNavigation.Handle("}");
        Assert.Equal(paragraphText.IndexOf("para3", StringComparison.Ordinal), paragraphEditor.CaretPosition);
        paragraphNavigation.Handle("{");
        Assert.Equal(paragraphText.IndexOf("para2", StringComparison.Ordinal), paragraphEditor.CaretPosition);
    }

    [Fact]
    public void WindowMotionsDelegateToViewportAdapter()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 0);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("2");
        navigation.Handle("H");
        navigation.Handle("M");
        navigation.Handle("3");
        navigation.Handle("L");

        Assert.Equal(
            new[]
            {
                (ViViewportTarget.Top, 2),
                (ViViewportTarget.Middle, 1),
                (ViViewportTarget.Bottom, 3)
            },
            editor.ViewportMoves);
    }

    [Fact]
    public void CtrlEAndCtrlYScrollViewWithoutChangingCoreCaret()
    {
        var editor = new FakeEditor("one\ntwo\nthree", 2);
        var navigation = new ViNavigationProcessor(editor);

        navigation.Handle("Ctrl+e");
        navigation.Handle("Ctrl+y");

        Assert.Equal(new[] { 1, -1 }, editor.ViewScrollDirections);
        Assert.Equal(2, editor.CaretPosition);
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
        public List<int> ViewScrollDirections { get; } = [];
        public List<(ViViewportTarget Target, int Count)> ViewportMoves { get; } = [];

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
        public void ScrollPage(int direction) => FullPageScrollDirections.Add(direction);
        public void ScrollHalfPage(int direction) => HalfPageScrollDirections.Add(direction);
        public void ScrollView(int direction) => ViewScrollDirections.Add(direction);
        public void MoveToViewport(ViViewportTarget target, int count = 1) => ViewportMoves.Add((target, count));
    }
}
