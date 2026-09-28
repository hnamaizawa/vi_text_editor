namespace ViTextEditor.Core.Editor;

public enum ViViewportTarget
{
    Top,
    Middle,
    Bottom
}

public interface IEditorAdapter
{
    string Text { get; }
    int CaretPosition { get; }
    int TextLength => Text.Length;

    int NextCharacterPosition(int position) => Math.Min(TextLength, Math.Max(0, position) + 1);

    string CharacterAt(int position)
    {
        var text = Text;
        if (position < 0 || position >= text.Length) return string.Empty;
        return char.IsHighSurrogate(text[position]) && position + 1 < text.Length && char.IsLowSurrogate(text[position + 1])
            ? text.Substring(position, 2)
            : text[position].ToString();
    }

    char CharAt(int position)
    {
        var text = Text;
        return position >= 0 && position < text.Length ? text[position] : '\0';
    }

    int LineStart(int position)
    {
        var text = Text;
        if (text.Length == 0) return 0;
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0) return 0;
        return text.LastIndexOf('\n', Math.Min(position - 1, text.Length - 1)) + 1;
    }

    int LineEndExclusive(int position)
    {
        var text = Text;
        if (text.Length == 0) return 0;
        var start = LineStart(position);
        var newline = text.IndexOf('\n', start);
        var end = newline < 0 ? text.Length : newline;
        if (end > start && text[end - 1] == '\r') end--;
        return end;
    }

    string GetTextRange(int position, int length)
    {
        var text = Text;
        position = Math.Clamp(position, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - position);
        return text.Substring(position, length);
    }

    void MoveCaret(int position);
    void DeleteRange(int position, int length);
    void InsertText(int position, string text);
    void ReplaceRange(int position, int length, string text)
    {
        DeleteRange(position, length);
        if (text.Length > 0) InsertText(position, text);
    }
    void Undo();
    void Redo();

    void ScrollPage(int direction)
    {
    }

    void ScrollHalfPage(int direction)
    {
    }

    void ScrollView(int direction)
    {
    }

    void MoveToViewport(ViViewportTarget target, int count = 1)
    {
    }
}
