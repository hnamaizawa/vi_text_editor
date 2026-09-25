using ScintillaNET;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal static class SyntaxHighlightingService
{
    private static readonly Color CommentColor = Color.FromArgb(0, 128, 0);
    private static readonly Color KeywordColor = Color.FromArgb(0, 0, 180);
    private static readonly Color StringColor = Color.FromArgb(163, 21, 21);
    private static readonly Color NumberColor = Color.FromArgb(128, 0, 128);
    private static readonly Color OperatorColor = Color.FromArgb(70, 70, 70);
    private static readonly Color HeadingColor = Color.FromArgb(0, 70, 140);
    private static readonly Color LinkColor = Color.FromArgb(0, 102, 204);
    private static readonly Color CodeColor = Color.FromArgb(120, 45, 0);
    private static readonly Color CodeBackColor = Color.FromArgb(245, 245, 245);
    private static readonly Color PropertyColor = Color.FromArgb(43, 145, 175);

    public static SyntaxLanguage Apply(Scintilla editor, string? path)
    {
        if (editor.IsDisposed || editor.Disposing) return SyntaxLanguage.PlainText;

        ResetStyles(editor);
        var language = SyntaxLanguageDetector.Detect(path);

        switch (language)
        {
            case SyntaxLanguage.Markdown:
                ConfigureMarkdown(editor);
                break;
            case SyntaxLanguage.Json:
                ConfigureJson(editor);
                break;
            case SyntaxLanguage.Xml:
                ConfigureXml(editor);
                break;
            case SyntaxLanguage.CSharp:
                ConfigureCSharp(editor);
                break;
            case SyntaxLanguage.Python:
                ConfigurePython(editor);
                break;
            case SyntaxLanguage.JavaScript:
                ConfigureJavaScript(editor, typeScript: false);
                break;
            case SyntaxLanguage.TypeScript:
                ConfigureJavaScript(editor, typeScript: true);
                break;
            case SyntaxLanguage.Yaml:
                ConfigureYaml(editor);
                break;
            default:
                editor.LexerName = string.Empty;
                break;
        }

        editor.Colorize(0, -1);
        return language;
    }

    private static void ResetStyles(Scintilla editor)
    {
        var defaultFont = editor.Styles[Style.Default].Font;
        var defaultSize = editor.Styles[Style.Default].SizeF;

        editor.LexerName = string.Empty;
        editor.Styles[Style.Default].ForeColor = SystemColors.WindowText;
        editor.Styles[Style.Default].BackColor = SystemColors.Window;
        editor.Styles[Style.Default].Bold = false;
        editor.Styles[Style.Default].Italic = false;
        if (!string.IsNullOrWhiteSpace(defaultFont)) editor.Styles[Style.Default].Font = defaultFont;
        if (defaultSize > 0) editor.Styles[Style.Default].SizeF = defaultSize;
        editor.StyleClearAll();

        editor.Styles[Style.LineNumber].ForeColor = SystemColors.GrayText;
        editor.Styles[Style.LineNumber].BackColor = SystemColors.Control;
    }

    private static void ConfigureMarkdown(Scintilla editor)
    {
        editor.LexerName = "markdown";

        SetStyle(editor, 2, HeadingColor, bold: true);  // strong **text**
        SetStyle(editor, 3, HeadingColor, bold: true);  // strong __text__
        SetStyle(editor, 4, SystemColors.WindowText, italic: true);
        SetStyle(editor, 5, SystemColors.WindowText, italic: true);

        for (var style = 6; style <= 11; style++)
        {
            SetStyle(editor, style, HeadingColor, bold: true);
            editor.Styles[style].SizeF = style switch
            {
                6 => 15f,
                7 => 14f,
                8 => 13f,
                _ => 12f
            };
        }

        SetStyle(editor, 12, HeadingColor, bold: true); // # marker
        SetStyle(editor, 13, KeywordColor, bold: true); // unordered list marker
        SetStyle(editor, 14, KeywordColor, bold: true); // ordered list marker
        SetStyle(editor, 15, SystemColors.GrayText, italic: true); // block quote
        // Lexilla's Markdown link style covers the complete Markdown construct
        // (`[label](url)`) and can extend into following full-width punctuation.
        // Keep that construct neutral; MainForm's URL indicators color and
        // underline only the exact http:// / https:// range.
        SetStyle(editor, 18, SystemColors.WindowText); // link construct
        for (var style = 19; style <= 21; style++) SetStyle(editor, style, CodeColor, backColor: CodeBackColor);
    }

    private static void ConfigureJson(Scintilla editor)
    {
        editor.LexerName = "json";
        SetStyle(editor, 1, NumberColor);     // number
        SetStyle(editor, 2, StringColor);     // string
        SetStyle(editor, 4, PropertyColor, bold: true); // property name
        SetStyle(editor, 5, OperatorColor);   // escape sequence
        SetStyle(editor, 6, CommentColor, italic: true); // line comment
        SetStyle(editor, 7, CommentColor, italic: true); // block comment
        SetStyle(editor, 8, OperatorColor);   // operator
        SetStyle(editor, 11, KeywordColor, bold: true); // true/false/null
    }

    private static void ConfigureXml(Scintilla editor)
    {
        editor.LexerName = "xml";
        SetStyle(editor, 1, KeywordColor, bold: true); // tag
        SetStyle(editor, 2, Color.Firebrick, bold: true); // unknown tag
        SetStyle(editor, 3, PropertyColor); // attribute
        SetStyle(editor, 4, Color.Firebrick); // unknown attribute
        SetStyle(editor, 5, NumberColor);
        SetStyle(editor, 6, StringColor);
        SetStyle(editor, 7, StringColor);
        SetStyle(editor, 9, CommentColor, italic: true);
        SetStyle(editor, 10, NumberColor); // entity
        SetStyle(editor, 17, SystemColors.GrayText); // CDATA
    }

    private static void ConfigureCSharp(Scintilla editor)
    {
        // Scintilla5.NET maps SCLEX_CSHARP to the Lexilla "cpp" lexer.
        editor.LexerName = "cpp";
        ConfigureCLikeStyles(editor);
        editor.SetKeywords(0,
            "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while async await dynamic get init required set value var when where with yield");
    }

    private static void ConfigureJavaScript(Scintilla editor, bool typeScript)
    {
        // Scintilla5.NET maps SCLEX_JAVASCRIPT to Lexilla "cpp" as well.
        editor.LexerName = "cpp";
        ConfigureCLikeStyles(editor);
        var keywords =
            "as async await break case catch class const continue debugger default delete do else export extends false finally for from function get if import in instanceof let new null of return set static super switch this throw true try typeof undefined var void while with yield";
        if (typeScript)
        {
            keywords += " abstract any assert asserts bigint boolean constructor declare enum implements infer interface keyof module namespace never number object private protected public readonly require string symbol type unknown override satisfies";
        }
        editor.SetKeywords(0, keywords);
    }

    private static void ConfigureCLikeStyles(Scintilla editor)
    {
        SetStyle(editor, 1, CommentColor, italic: true);
        SetStyle(editor, 2, CommentColor, italic: true);
        SetStyle(editor, 3, CommentColor, italic: true);
        SetStyle(editor, 4, NumberColor);
        SetStyle(editor, 5, KeywordColor, bold: true);
        SetStyle(editor, 6, StringColor);
        SetStyle(editor, 7, StringColor);
        SetStyle(editor, 9, PropertyColor);
        SetStyle(editor, 10, OperatorColor);
        SetStyle(editor, 13, StringColor);
        SetStyle(editor, 14, StringColor);
        SetStyle(editor, 15, CommentColor, italic: true);
        SetStyle(editor, 16, KeywordColor, bold: true);
        SetStyle(editor, 20, StringColor);
        SetStyle(editor, 27, StringColor);
    }

    private static void ConfigurePython(Scintilla editor)
    {
        editor.LexerName = "python";
        SetStyle(editor, 1, CommentColor, italic: true);
        SetStyle(editor, 2, NumberColor);
        SetStyle(editor, 3, StringColor);
        SetStyle(editor, 4, StringColor);
        SetStyle(editor, 5, KeywordColor, bold: true);
        SetStyle(editor, 6, StringColor);
        SetStyle(editor, 7, StringColor);
        SetStyle(editor, 8, PropertyColor, bold: true);
        SetStyle(editor, 9, PropertyColor, bold: true);
        SetStyle(editor, 10, OperatorColor);
        SetStyle(editor, 12, CommentColor, italic: true);
        SetStyle(editor, 14, KeywordColor, bold: true);
        SetStyle(editor, 15, PropertyColor);
        for (var style = 16; style <= 19; style++) SetStyle(editor, style, StringColor);
        editor.SetKeywords(0,
            "and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield match case");
    }

    private static void ConfigureYaml(Scintilla editor)
    {
        editor.LexerName = "yaml";
        SetStyle(editor, 1, CommentColor, italic: true);
        SetStyle(editor, 2, PropertyColor, bold: true);
        SetStyle(editor, 3, KeywordColor, bold: true);
        SetStyle(editor, 4, NumberColor);
        SetStyle(editor, 5, LinkColor);
        SetStyle(editor, 6, KeywordColor, bold: true);
        SetStyle(editor, 7, StringColor);
        SetStyle(editor, 8, Color.Firebrick, bold: true);
        SetStyle(editor, 9, OperatorColor);
        editor.SetKeywords(0, "true false null yes no on off True False Null YES NO ON OFF");
    }

    private static void SetStyle(Scintilla editor, int index, Color foreColor, bool bold = false, bool italic = false, Color? backColor = null)
    {
        editor.Styles[index].ForeColor = foreColor;
        editor.Styles[index].Bold = bold;
        editor.Styles[index].Italic = italic;
        if (backColor.HasValue) editor.Styles[index].BackColor = backColor.Value;
    }
}
