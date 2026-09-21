namespace ViTextEditor;

/// <summary>
/// Application-local MessageBox facade. Windows plays a system sound when a
/// native message box uses Error/Warning icons. Keep the dialog text/buttons
/// intact while dropping only those sound-producing icon flags.
/// </summary>
internal static class MessageBox
{
    public static DialogResult Show(string text) =>
        System.Windows.Forms.MessageBox.Show(text);

    public static DialogResult Show(string text, string caption) =>
        System.Windows.Forms.MessageBox.Show(text, caption);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) =>
        System.Windows.Forms.MessageBox.Show(text, caption, buttons);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        System.Windows.Forms.MessageBox.Show(text, caption, buttons, SilentIcon(icon));

    public static DialogResult Show(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton) =>
        System.Windows.Forms.MessageBox.Show(text, caption, buttons, SilentIcon(icon), defaultButton);

    public static DialogResult Show(IWin32Window owner, string text) =>
        System.Windows.Forms.MessageBox.Show(owner, text);

    public static DialogResult Show(IWin32Window owner, string text, string caption) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons);

    public static DialogResult Show(
        IWin32Window owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons, SilentIcon(icon));

    public static DialogResult Show(
        IWin32Window owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton) =>
        System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons, SilentIcon(icon), defaultButton);

    private static MessageBoxIcon SilentIcon(MessageBoxIcon icon) =>
        icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning
            ? MessageBoxIcon.None
            : icon;
}
