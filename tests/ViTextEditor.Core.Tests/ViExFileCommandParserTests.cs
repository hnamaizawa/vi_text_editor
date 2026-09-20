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
    [InlineData("q!", ViExFileCommandKind.QuitForce)]
    [InlineData("quit!", ViExFileCommandKind.QuitForce)]
    [InlineData("w", ViExFileCommandKind.WriteCurrent)]
    [InlineData("write", ViExFileCommandKind.WriteCurrent)]
    public void ParsesCommandsWithoutArguments(string text, ViExFileCommandKind expectedKind)
    {
        Assert.True(ViExFileCommandParser.TryParse(text, out var command));
        Assert.Equal(expectedKind, command.Kind);
        Assert.Null(command.Argument);
    }

    [Theory]
    [InlineData("w copy.txt", "copy.txt")]
    [InlineData("write C:\\temp\\copy file.txt", "C:\\temp\\copy file.txt")]
    public void ParsesWriteAsWithFullRemainingPath(string text, string expectedPath)
    {
        Assert.True(ViExFileCommandParser.TryParse(text, out var command));
        Assert.Equal(ViExFileCommandKind.WriteAs, command.Kind);
        Assert.Equal(expectedPath, command.Argument);
    }

    [Fact]
    public void UnknownCommandIsNotClaimed()
    {
        Assert.False(ViExFileCommandParser.TryParse("set number", out _));
    }
}
