using ScintillaNET;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal sealed class ScintillaEditorAdapter : IEditorAdapter
{
    private readonly Scintilla _editor;

    public ScintillaEditorAdapter(Scintilla editor)
    {
        _editor = editor;
    }

    public string Text => _editor.Text;
    public int CaretPosition => _editor.CurrentPosition;

    public void MoveCaret(int position)
    {
        _editor.GotoPosition(Math.Clamp(position, 0, _editor.TextLength));
    }

    public void DeleteRange(int position, int length)
    {
        _editor.DeleteRange(position, length);
    }

    public void InsertText(int position, string text)
    {
        _editor.GotoPosition(Math.Clamp(position, 0, _editor.TextLength));
        _editor.AddText(text);
    }

    public void Undo() => _editor.Undo();
    public void Redo() => _editor.Redo();

    public void ScrollPage(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        var pageLines = Math.Max(1, _editor.LinesOnScreen - 2);
        var target = _editor.FirstVisibleLine + (Math.Sign(direction) * pageLines);
        var maxFirstVisibleLine = Math.Max(0, _editor.Lines.Count - 1);
        _editor.FirstVisibleLine = Math.Clamp(target, 0, maxFirstVisibleLine);
    }
}
