using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class ViExFileCommandParserTests
{
    [Theory]
    [InlineData("e!", ViExFileCommandKind.ReloadForce)]
    [InlineData("edit!", ViExFileCommandKind.ReloadForce)]
    [InlineData("e#", ViExFileCommandKind.EditAlternate)]
    [InlineData("e #", ViExFileCommandKind.EditAlternate)]
    [InlineData("q", ViExFileCommandKind.Quit)]
    [InlineData("quit", ViExFileCommandKind.Quit)]
    [InlineData("q!", ViExFileCommandKind.QuitForce)]
    [InlineData("quit!", ViExFileCommandKind.QuitForce)]
    [InlineData("w", ViExFileCommandKind.WriteCurrent)]
    [InlineData("write", ViExFileCommandKind.WriteCurrent)]
    [InlineData("w!", ViExFileCommandKind.WriteCurrentForce)]
    [InlineData("wq", ViExFileCommandKind.WriteQuit)]
    [InlineData("wq!", ViExFileCommandKind.WriteQuitForce)]
    [InlineData("x", ViExFileCommandKind.Xit)]
    [InlineData("xit!", ViExFileCommandKind.XitForce)]
    public void ParsesCommandsWithoutArguments(string text, ViExFileCommandKind expectedKind)
    {
        Assert.True(ViExFileCommandParser.TryParse(text, out var command));
        Assert.Equal(expectedKind, command.Kind);
        Assert.Null(command.Argument);
    }

    [Theory]
    [InlineData("w copy.txt", ViExFileCommandKind.WriteFile, "copy.txt")]
    [InlineData("write C:\\temp\\copy file.txt", ViExFileCommandKind.WriteFile, "C:\\temp\\copy file.txt")]
    [InlineData("w! copy.txt", ViExFileCommandKind.WriteFileForce, "copy.txt")]
    [InlineData("e other.txt", ViExFileCommandKind.Edit, "other.txt")]
    [InlineData("edit! other.txt", ViExFileCommandKind.EditForce, "other.txt")]
    [InlineData("saveas renamed.txt", ViExFileCommandKind.SaveAs, "renamed.txt")]
    [InlineData("sav! renamed.txt", ViExFileCommandKind.SaveAsForce, "renamed.txt")]
    [InlineData("wq copy.txt", ViExFileCommandKind.WriteQuit, "copy.txt")]
    [InlineData("x copy.txt", ViExFileCommandKind.Xit, "copy.txt")]
    public void ParsesCommandsWithFullRemainingPath(string text, ViExFileCommandKind expectedKind, string expectedPath)
    {
        Assert.True(ViExFileCommandParser.TryParse(text, out var command));
        Assert.Equal(expectedKind, command.Kind);
        Assert.Equal(expectedPath, command.Argument);
    }

    [Fact]
    public void UnknownCommandIsNotClaimed()
    {
        Assert.False(ViExFileCommandParser.TryParse("set number", out _));
        Assert.False(ViExFileCommandParser.TryParse("substitute/foo/bar/", out _));
    }
}
