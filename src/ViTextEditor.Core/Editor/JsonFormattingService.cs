using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ViTextEditor.Core.Editor;

public static class JsonFormattingService
{
    public static bool TryFormat(string json, string newLine, out string formatted, out string? error)
    {
        formatted = json;
        error = null;

        if (TryParseAndFormat(json, newLine, out formatted, out var firstError))
        {
            return true;
        }

        // Some machine-generated "JSON" contains a BOM, non-JSON Unicode spaces,
        // or whitespace between a numeric sign and its digits (for example - 123).
        // These are unambiguous to normalize outside quoted strings, while the
        // original strict parse remains the first choice for standards-compliant JSON.
        var normalized = NormalizeMachineGeneratedJson(json);
        if (!string.Equals(normalized, json, StringComparison.Ordinal) &&
            TryParseAndFormat(normalized, newLine, out formatted, out var normalizedError))
        {
            return true;
        }

        formatted = json;
        error = $"JSONの解析に失敗しました: {firstError}";
        return false;
    }

    private static bool TryParseAndFormat(string json, string newLine, out string formatted, out string? error)
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
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // Keep Japanese and other readable Unicode characters as-is instead of
                // escaping them to \uXXXX sequences. JSON-required escaping (quotes,
                // backslashes, control characters, etc.) is still preserved.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            formatted = JsonSerializer.Serialize(document.RootElement, options);
            if (newLine != "\n") formatted = formatted.Replace("\n", newLine, StringComparison.Ordinal);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string NormalizeMachineGeneratedJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        var builder = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                builder.Append(c);
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                builder.Append(c);
                continue;
            }

            if (c == '\uFEFF' && builder.Length == 0)
            {
                continue;
            }

            if (char.IsWhiteSpace(c) && c is not (' ' or '\t' or '\r' or '\n'))
            {
                builder.Append(' ');
                continue;
            }

            if (c is '+' or '-' or '\u2212' or '\uFF0B' or '\uFF0D')
            {
                var negative = c is '-' or '\u2212' or '\uFF0D';
                var lookahead = i + 1;
                while (lookahead < json.Length && char.IsWhiteSpace(json[lookahead])) lookahead++;

                if (lookahead < json.Length && (char.IsDigit(json[lookahead]) || json[lookahead] == '.'))
                {
                    if (negative) builder.Append('-');
                    if (json[lookahead] == '.') builder.Append('0');
                    i = lookahead - 1;
                    continue;
                }
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
