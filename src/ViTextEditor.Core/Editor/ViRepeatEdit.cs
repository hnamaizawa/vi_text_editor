namespace ViTextEditor.Core.Editor;

public readonly record struct ViRepeatEdit(bool IsInsert, int RelativePosition, string Text)
{
    public static ViRepeatEdit Insert(int relativePosition, string text) => new(true, relativePosition, text);
    public static ViRepeatEdit Delete(int relativePosition, string text) => new(false, relativePosition, text);
}
