using System.Text.RegularExpressions;

namespace ViTextEditor.Core.IO;

public static partial class UrlDetectionService
{
    public readonly record struct UrlMatch(string Value, int Start, int Length);

    public static IReadOnlyList<UrlMatch> FindAll(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var results = new List<UrlMatch>();
        foreach (Match match in HttpUrlRegex().Matches(text))
        {
            var value = TrimTrailingPunctuation(match.Value);
            if (!IsSupportedUrl(value)) continue;
            results.Add(new UrlMatch(value, match.Index, value.Length));
        }

        return results;
    }

    public static UrlMatch? FindAt(string text, int characterIndex)
    {
        if (string.IsNullOrEmpty(text) || characterIndex < 0 || characterIndex >= text.Length) return null;

        foreach (var match in FindAll(text))
        {
            if (characterIndex >= match.Start && characterIndex < match.Start + match.Length) return match;
        }

        return null;
    }

    private static bool IsSupportedUrl(string value)
    {
        if (value.Length == 0 || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingPunctuation(string value)
    {
        value = TrimAtFirstUnmatchedClosingDelimiter(value);
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

    private static string TrimAtFirstUnmatchedClosingDelimiter(string value)
    {
        var round = 0;
        var curly = 0;
        var fullWidthRound = 0;
        var fullWidthCurly = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                    round++;
                    break;
                case ')':
                    if (round == 0) return value[..index];
                    round--;
                    break;
                case '{':
                    curly++;
                    break;
                case '}':
                    if (curly == 0) return value[..index];
                    curly--;
                    break;
                case '（':
                    fullWidthRound++;
                    break;
                case '）':
                    if (fullWidthRound == 0) return value[..index];
                    fullWidthRound--;
                    break;
                case '｛':
                    fullWidthCurly++;
                    break;
                case '｝':
                    if (fullWidthCurly == 0) return value[..index];
                    fullWidthCurly--;
                    break;
            }
        }
        return value;
    }

    // Raw square brackets delimit Markdown link labels. A literal square bracket
    // inside a URL must be percent-encoded, so stopping here also prevents one
    // Markdown link from being treated as a single oversized URL.
    [GeneratedRegex("https?://[^\\s<>\\\"'\\[\\]]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlRegex();
}
