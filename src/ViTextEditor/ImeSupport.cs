using ScintillaNET;

namespace ViTextEditor;

internal static class ImeSupport
{
    private const int SciSetImeInteraction = 2679;
    private const int ScImeInline = 1;

    public static void Configure(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Scintilla scintilla)
            {
                scintilla.ImeMode = ImeMode.NoControl;
                scintilla.HandleCreated += (_, _) => EnableInlineIme(scintilla);
                if (scintilla.IsHandleCreated) EnableInlineIme(scintilla);
            }
            if (control.HasChildren) Configure(control);
        }
    }

    private static void EnableInlineIme(Scintilla scintilla) =>
        scintilla.DirectMessage(SciSetImeInteraction, new IntPtr(ScImeInline));
}
