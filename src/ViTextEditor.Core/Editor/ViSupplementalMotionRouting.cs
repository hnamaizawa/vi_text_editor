namespace ViTextEditor.Core.Editor;

public static class ViSupplementalMotionRouting
{
    public static bool IsCommandOrSearchPrefix(char value) => value is ':' or '/' or '?';

    public static bool TryGetPunctuationMotion(char value, out string token)
    {
        token = value switch
        {
            '%' => "%",
            ';' => ";",
            ',' => ",",
            '(' => "(",
            ')' => ")",
            '{' => "{",
            '}' => "}",
            '+' => "+",
            '-' => "-",
            '_' => "_",
            '|' => "|",
            _ => string.Empty
        };

        return token.Length != 0;
    }
}
