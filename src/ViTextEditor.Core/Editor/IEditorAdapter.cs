namespace ViTextEditor.Core.Editor;

public interface IEditorAdapter
{
    string Text { get; }
    int CaretPosition { get; }
    void MoveCaret(int position);
    void DeleteRange(int position, int length);
    void InsertText(int position, string text);
    void Undo();
    void Redo();
}
