using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class UrlDetectionServiceTests
{
    [Fact]
    public void FindAll_ReturnsEverySupportedUrlWithCharacterRanges()
    {
        const string text = "日本語 https://uipath.com と http://example.jp/path。";

        var results = UrlDetectionService.FindAll(text);

        Assert.Equal(2, results.Count);
        Assert.Equal(new UrlDetectionService.UrlMatch("https://uipath.com", 4, 18), results[0]);
        Assert.Equal(new UrlDetectionService.UrlMatch("http://example.jp/path", 25, 22), results[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://example.com/file")]
    [InlineData("URLではありません")]
    public void FindAll_ReturnsEmptyWhenNoSupportedUrlExists(string text)
    {
        Assert.Empty(UrlDetectionService.FindAll(text));
    }

    [Theory]
    [InlineData("https://example.com/path", 0, "https://example.com/path")]
    [InlineData("日本語 https://example.com/path?q=1&x=2 です", 7, "https://example.com/path?q=1&x=2")]
    [InlineData("参照: http://example.jp/test。", 8, "http://example.jp/test")]
    [InlineData("(https://example.com/path)", 5, "https://example.com/path")]
    [InlineData("https://en.wikipedia.org/wiki/Function_(mathematics)", 20, "https://en.wikipedia.org/wiki/Function_(mathematics)")]
    [InlineData("HTTPS://EXAMPLE.COM/A", 10, "HTTPS://EXAMPLE.COM/A")]
    public void FindAt_ReturnsHttpUrlUnderCharacter(string text, int index, string expected)
    {
        var result = UrlDetectionService.FindAt(text, index);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("https://example.com", 19)]
    [InlineData("prefix https://example.com suffix", 2)]
    [InlineData("ftp://example.com/file", 8)]
    [InlineData("not a url", 3)]
    public void FindAt_ReturnsNullOutsideSupportedUrl(string text, int index)
    {
        Assert.Null(UrlDetectionService.FindAt(text, index));
    }
}
