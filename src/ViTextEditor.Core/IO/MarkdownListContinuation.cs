namespace ViTextEditor.Core.IO;

public static class MarkdownListContinuation
{
    public static bool TryCreatePrefix(string lineText, out string prefix)
    {
        prefix = string.Empty;
        if (lineText is null) return false;

        var marker = 0;
        while (marker < lineText.Length && lineText[marker] is ' ' or '\t') marker++;
        if (marker >= lineText.Length || lineText[marker] != '-') return false;
        if (marker + 1 < lineText.Length && lineText[marker + 1] is not (' ' or '\t')) return false;
        if (lineText[(marker + 1)..].All(char.IsWhiteSpace)) return false;

        prefix = lineText[..marker] + "  - ";
        return true;
    }
}
