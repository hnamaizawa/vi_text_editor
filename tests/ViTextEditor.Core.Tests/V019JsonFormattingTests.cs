using System.Text.Json;
using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class V019JsonFormattingTests
{
    [Fact]
    public void JsonFormattingKeepsJapaneseReadableInsteadOfUnicodeEscapes()
    {
        const string source = "{\"名前\":\"山田太郎\",\"住所\":\"東京都千代田区\"}";

        Assert.True(JsonFormattingService.TryFormat(source, "\r\n", out var formatted, out var error));
        Assert.Null(error);
        Assert.Contains("\"名前\": \"山田太郎\"", formatted);
        Assert.Contains("\"住所\": \"東京都千代田区\"", formatted);
        Assert.DoesNotContain("\\u5C71", formatted);
        Assert.DoesNotContain("\\u6771", formatted);

        using var parsed = JsonDocument.Parse(formatted);
        Assert.Equal("山田太郎", parsed.RootElement.GetProperty("名前").GetString());
    }

    [Fact]
    public void JsonFormattingStillEscapesCharactersRequiredByJson()
    {
        const string source = "{\"message\":\"日本語\\n\\\"quoted\\\"\\\\path\"}";

        Assert.True(JsonFormattingService.TryFormat(source, "\n", out var formatted, out var error));
        Assert.Null(error);
        Assert.Contains("日本語", formatted);
        Assert.Contains("\\n", formatted);
        Assert.Contains("\\\"quoted\\\"", formatted);
        Assert.Contains("\\\\path", formatted);
    }
}
