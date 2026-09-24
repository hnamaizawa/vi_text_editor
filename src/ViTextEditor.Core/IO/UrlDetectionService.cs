using System.Text.RegularExpressions;

namespace ViTextEditor.Core.IO;

public static partial class UrlDetectionService
{
    public readonly record struct UrlMatch(string Value, int Start, int Length);

    public static UrlMatch? FindAt(string text, int characterIndex)
    {
        if (string.IsNullOrEmpty(text) || characterIndex < 0 || characterIndex >= text.Length) return null;

        foreach (Match match in HttpUrlRegex().Matches(text))
        {
            var value = TrimTrailingPunctuation(match.Value);
            if (value.Length == 0 || characterIndex < match.Index || characterIndex >= match.Index + value.Length) continue;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) continue;
            if (uri.Scheme is not (Uri.UriSchemeHttp or Uri.UriSchemeHttps)) continue;
            return new UrlMatch(value, match.Index, value.Length);
        }

        return null;
    }

    private static string TrimTrailingPunctuation(string value)
    {
        while (value.Length > 0)
        {
            var last = value[^1];
            if (".,;:!?。、，；：！？」』】".Contains(last))
            {
                value = value[..^1];
                continue;
            }

            var opening = last switch
            {
                ')' => '(',
                ']' => '[',
                '}' => '{',
                '）' => '（',
                '］' => '［',
                '｝' => '｛',
                _ => '\0'
            };
            if (opening != '\0' && value.Count(c => c == last) > value.Count(c => c == opening))
            {
                value = value[..^1];
                continue;
            }

            break;
        }
        return value;
    }

    [GeneratedRegex("https?://[^\\s<>\\\"']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlRegex();
}
