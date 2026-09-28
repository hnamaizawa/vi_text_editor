using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class MarkdownListContinuationTests
{
    [Theory]
    [InlineData("- item", "  - ")]
    [InlineData("  - item", "    - ")]
    [InlineData("\t- item", "\t  - ")]
    [InlineData("-", "  - ")]
    public void TryCreatePrefix_IndentsNestedBulletByTwoCharacters(string line, string expected)
    {
        Assert.True(MarkdownListContinuation.TryCreatePrefix(line, out var prefix));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("heading - item")]
    [InlineData("-not-a-list")]
    [InlineData("")]
    public void TryCreatePrefix_IgnoresNonListLines(string line)
    {
        Assert.False(MarkdownListContinuation.TryCreatePrefix(line, out var prefix));
        Assert.Empty(prefix);
    }
}
