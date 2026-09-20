using System.Text.Json;

namespace ViTextEditor.Core.Editor;

public static class JsonFormattingService
{
    public static bool TryFormat(string json, string newLine, out string formatted, out string? error)
    {
        formatted = json;
        error = null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var options = new JsonSerializerOptions { WriteIndented = true };
            formatted = JsonSerializer.Serialize(document.RootElement, options);
            if (newLine != "\n") formatted = formatted.Replace("\n", newLine, StringComparison.Ordinal);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSONの解析に失敗しました: {ex.Message}";
            return false;
        }
    }
}
