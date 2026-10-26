using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class MarkdownListContinuationTests
{
    [Theory]
    [InlineData("- item", "  - ")]
    [InlineData("  - item", "    - ")]
    [InlineData("\t- item", "\t  - ")]
    [InlineData("- aaa", "  - ")]
    [InlineData("  - 日本語", "    - ")]
    public void TryCreatePrefix_IndentsNestedBulletByTwoCharacters(string line, string expected)
    {
        Assert.True(MarkdownListContinuation.TryCreatePrefix(line, out var prefix));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("heading - item")]
    [InlineData("-not-a-list")]
    [InlineData("-")]
    [InlineData("- ")]
    [InlineData("  -   ")]
    [InlineData("\t-\t")]
    [InlineData("")]
    public void TryCreatePrefix_IgnoresNonListLines(string line)
    {
        Assert.False(MarkdownListContinuation.TryCreatePrefix(line, out var prefix));
        Assert.Empty(prefix);
    }

    [Theory]
    [InlineData("- item", "- ")]
    [InlineData("  - item", "  - ")]
    [InlineData("\t- item", "\t- ")]
    [InlineData("    - 日本語", "    - ")]
    public void TryCreateSiblingPrefix_PreservesCurrentIndentation(string line, string expected)
    {
        Assert.True(MarkdownListContinuation.TryCreateSiblingPrefix(line, out var prefix));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("-not-a-list")]
    [InlineData("-")]
    [InlineData("- ")]
    [InlineData("  -   ")]
    public void TryCreateSiblingPrefix_IgnoresNonListAndEmptyItems(string line)
    {
        Assert.False(MarkdownListContinuation.TryCreateSiblingPrefix(line, out var prefix));
        Assert.Empty(prefix);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("- ")]
    [InlineData("  -   ")]
    [InlineData("\t-\t")]
    public void IsEmptyListItem_DetectsEmptyBulletLines(string line)
    {
        Assert.True(MarkdownListContinuation.IsEmptyListItem(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("- item")]
    [InlineData("  - item")]
    [InlineData("-not-a-list")]
    [InlineData("heading - ")]
    public void IsEmptyListItem_IgnoresContentAndNonListLines(string line)
    {
        Assert.False(MarkdownListContinuation.IsEmptyListItem(line));
    }
}
