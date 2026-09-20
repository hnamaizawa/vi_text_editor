using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class SyntaxLanguageDetectorTests
{
    [Theory]
    [InlineData("memo.md", SyntaxLanguage.Markdown)]
    [InlineData("memo.markdown", SyntaxLanguage.Markdown)]
    [InlineData("settings.json", SyntaxLanguage.Json)]
    [InlineData("layout.xaml", SyntaxLanguage.Xml)]
    [InlineData("Program.cs", SyntaxLanguage.CSharp)]
    [InlineData("script.py", SyntaxLanguage.Python)]
    [InlineData("app.js", SyntaxLanguage.JavaScript)]
    [InlineData("app.tsx", SyntaxLanguage.TypeScript)]
    [InlineData("config.yaml", SyntaxLanguage.Yaml)]
    [InlineData("notes.txt", SyntaxLanguage.PlainText)]
    public void DetectUsesFileExtension(string path, SyntaxLanguage expected)
    {
        Assert.Equal(expected, SyntaxLanguageDetector.Detect(path));
    }

    [Fact]
    public void DetectIsCaseInsensitive()
    {
        Assert.Equal(SyntaxLanguage.Markdown, SyntaxLanguageDetector.Detect("README.MD"));
        Assert.Equal(SyntaxLanguage.Json, SyntaxLanguageDetector.Detect("DATA.JSON"));
    }
}
