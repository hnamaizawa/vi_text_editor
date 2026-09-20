namespace ViTextEditor.Core.IO;

public enum SyntaxLanguage
{
    PlainText,
    Markdown,
    Json,
    Xml,
    CSharp,
    Python,
    JavaScript,
    TypeScript,
    Yaml
}

public static class SyntaxLanguageDetector
{
    public static SyntaxLanguage Detect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return SyntaxLanguage.PlainText;

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => SyntaxLanguage.Markdown,
            ".json" or ".jsonl" => SyntaxLanguage.Json,
            ".xml" or ".xaml" or ".svg" => SyntaxLanguage.Xml,
            ".cs" or ".csx" => SyntaxLanguage.CSharp,
            ".py" or ".pyw" => SyntaxLanguage.Python,
            ".js" or ".jsx" or ".mjs" or ".cjs" => SyntaxLanguage.JavaScript,
            ".ts" or ".tsx" => SyntaxLanguage.TypeScript,
            ".yaml" or ".yml" => SyntaxLanguage.Yaml,
            _ => SyntaxLanguage.PlainText
        };
    }
}
