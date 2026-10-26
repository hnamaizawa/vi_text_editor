namespace ViTextEditor.Core.IO;

public static class MarkdownListContinuation
{
    public static bool IsEmptyListItem(string lineText)
    {
        if (lineText is null) return false;

        var marker = FindListMarker(lineText);
        return marker >= 0 && lineText[(marker + 1)..].All(char.IsWhiteSpace);
    }

    public static bool TryCreatePrefix(string lineText, out string prefix)
    {
        prefix = string.Empty;
        if (lineText is null) return false;

        var marker = FindListMarker(lineText);
        if (marker < 0) return false;
        if (lineText[(marker + 1)..].All(char.IsWhiteSpace)) return false;

        prefix = lineText[..marker] + "  - ";
        return true;
    }

    public static bool TryCreateSiblingPrefix(string lineText, out string prefix)
    {
        prefix = string.Empty;
        if (lineText is null) return false;

        var marker = FindListMarker(lineText);
        if (marker < 0) return false;
        if (lineText[(marker + 1)..].All(char.IsWhiteSpace)) return false;

        prefix = lineText[..marker] + "- ";
        return true;
    }

    private static int FindListMarker(string lineText)
    {
        var marker = 0;
        while (marker < lineText.Length && lineText[marker] is ' ' or '\t') marker++;
        if (marker >= lineText.Length || lineText[marker] != '-') return -1;
        if (marker + 1 < lineText.Length && lineText[marker + 1] is not (' ' or '\t')) return -1;
        return marker;
    }
}
