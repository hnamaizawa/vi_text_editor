using System.Text;
using ScintillaNET;

namespace ViTextEditor;

internal static class Program
{
    private const int SciSetImeInteraction = 2679;
    private const int ScImeInline = 1;

    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        var mainForm = new MainForm();
        ConfigureIme(mainForm);
        Application.Run(mainForm);
    }

    private static void ConfigureIme(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Scintilla scintilla)
            {
                scintilla.ImeMode = ImeMode.NoControl;
                scintilla.HandleCreated += (_, _) => EnableInlineIme(scintilla);

                if (scintilla.IsHandleCreated)
                {
                    EnableInlineIme(scintilla);
                }
            }

            if (control.HasChildren)
            {
                ConfigureIme(control);
            }
        }
    }

    private static void EnableInlineIme(Scintilla scintilla)
    {
        // Scintilla's inline IME renders the composition string at the editor
        // caret instead of using a separate composition window. This keeps
        // Japanese IME conversion text aligned with the actual input position.
        scintilla.DirectMessage(SciSetImeInteraction, new IntPtr(ScImeInline));
    }
}
