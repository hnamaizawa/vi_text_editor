using System.Reflection;
using ScintillaNET;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal static class MainFormWorkspaceBridge
{
    private const int CoordinateOverlayWidth = 190;

    private static readonly FieldInfo? FilePathField = typeof(MainForm).GetField("_filePath", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NewLineField = typeof(MainForm).GetField("_newLine", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ForceCloseField = typeof(MainForm).GetField("_forceClose", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? OpenFilePathMethod = typeof(MainForm).GetMethod("OpenFilePath", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool OpenPath(MainForm form, string path)
    {
        try
        {
            return OpenFilePathMethod?.Invoke(form, [path, true]) as bool? ?? false;
        }
        catch (TargetInvocationException ex)
        {
            MessageBox.Show(form, ex.InnerException?.Message ?? ex.Message, "ファイルを開けません", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public static string? GetFilePath(MainForm form) => FilePathField?.GetValue(form) as string;

    public static Scintilla? GetEditor(MainForm form) => FindControls<Scintilla>(form).FirstOrDefault();

    public static void AllowDeferredClose(MainForm form)
    {
        if (form.IsDisposed) return;
        ForceCloseField?.SetValue(form, true);
    }

    public static void Install(MainForm form, EditorWorkspaceForm workspace)
    {
        var menu = form.MainMenuStrip ?? FindControls<MenuStrip>(form).FirstOrDefault();
        if (menu is null) return;

        InstallFileMenu(form, workspace, menu);
        InstallTabMenu(workspace, menu);
        InstallToolsMenu(form, workspace, menu);
        InstallHelpVersion(menu);
        InstallCoordinateUpdater(form, workspace);
    }

    private static void InstallFileMenu(MainForm form, EditorWorkspaceForm workspace, MenuStrip menu)
    {
        var file = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text.Contains("ファイル", StringComparison.Ordinal));
        if (file is null) return;

        ReplaceMenuItem(file, item => item.Text.StartsWith("新規", StringComparison.Ordinal),
            new ToolStripMenuItem("新しいタブ(&N)", null, (_, _) => workspace.NewTab(), Keys.Control | Keys.N));

        var openItem = new ToolStripMenuItem("開く(&O)...", null, (_, _) => workspace.OpenFileFromDialog(), Keys.Control | Keys.O);
        ReplaceMenuItem(file, item => item.Text.StartsWith("開く", StringComparison.Ordinal), openItem);

        var recent = new ToolStripMenuItem("最近使ったファイル(&R)");
        recent.DropDownOpening += (_, _) => PopulateRecentFiles(recent, workspace);
        var openIndex = file.DropDownItems.IndexOf(openItem);
        file.DropDownItems.Insert(Math.Min(file.DropDownItems.Count, Math.Max(0, openIndex + 1)), recent);

        ReplaceMenuItem(file, item => item.Text.StartsWith("終了", StringComparison.Ordinal),
            new ToolStripMenuItem("終了(&X)", null, (_, _) => workspace.ExitApplication()));
    }

    private static void PopulateRecentFiles(ToolStripMenuItem recent, EditorWorkspaceForm workspace)
    {
        recent.DropDownItems.Clear();
        var files = workspace.RecentFiles.Files;
        if (files.Count == 0)
        {
            recent.DropDownItems.Add(new ToolStripMenuItem("(履歴なし)") { Enabled = false });
            return;
        }

        for (var i = 0; i < files.Count; i++)
        {
            var path = files[i];
            var label = $"{i + 1}. {Path.GetFileName(path)}    {Path.GetDirectoryName(path)}";
            var item = new ToolStripMenuItem(label) { ToolTipText = path };
            item.Click += (_, _) => workspace.OpenPath(path);
            recent.DropDownItems.Add(item);
        }
    }

    private static void InstallTabMenu(EditorWorkspaceForm workspace, MenuStrip menu)
    {
        var existing = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text.Contains("タブ", StringComparison.Ordinal));
        if (existing is not null) return;
        var tabs = new ToolStripMenuItem("タブ(&B)");
        tabs.DropDownItems.Add(new ToolStripMenuItem("新しいタブ", null, (_, _) => workspace.NewTab(), Keys.Control | Keys.T));
        tabs.DropDownItems.Add(new ToolStripMenuItem("現在のタブを閉じる", null, (_, _) => workspace.CloseCurrentTab(), Keys.Control | Keys.W));
        tabs.DropDownItems.Add(new ToolStripSeparator());
        tabs.DropDownItems.Add(new ToolStripMenuItem("次のタブ", null, (_, _) => workspace.SelectNextTab(1), Keys.Control | Keys.Tab));
        tabs.DropDownItems.Add(new ToolStripMenuItem("前のタブ", null, (_, _) => workspace.SelectNextTab(-1), Keys.Control | Keys.Shift | Keys.Tab));
        var helpIndex = menu.Items.OfType<ToolStripMenuItem>().Select((item, index) => (item, index)).FirstOrDefault(x => x.item.Text.Contains("ヘルプ", StringComparison.Ordinal)).index;
        menu.Items.Insert(helpIndex > 0 ? helpIndex : Math.Max(0, menu.Items.Count - 1), tabs);
    }

    private static void InstallToolsMenu(MainForm form, EditorWorkspaceForm workspace, MenuStrip menu)
    {
        var tools = new ToolStripMenuItem("ツール(&T)");
        tools.DropDownItems.Add(new ToolStripMenuItem("JSONを整形(&J)", null, (_, _) => FormatJson(form), Keys.Control | Keys.Shift | Keys.J));
        tools.DropDownItems.Add(new ToolStripMenuItem("Markdownプレビュー(&M)", null, (_, _) => workspace.OpenMarkdownPreview(form), Keys.Control | Keys.Shift | Keys.M));
        var helpIndex = menu.Items.OfType<ToolStripMenuItem>().Select((item, index) => (item, index)).FirstOrDefault(x => x.item.Text.Contains("ヘルプ", StringComparison.Ordinal)).index;
        menu.Items.Insert(helpIndex > 0 ? helpIndex : Math.Max(0, menu.Items.Count - 1), tools);
    }

    private static void FormatJson(MainForm form)
    {
        var editor = GetEditor(form);
        if (editor is null || editor.IsDisposed) return;
        if (editor.ReadOnly)
        {
            MessageBox.Show(form, "JSONを整形するには参照モードをOFFにしてください。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newline = NewLineField?.GetValue(form) as string ?? Environment.NewLine;
        if (!JsonFormattingService.TryFormat(editor.Text, newline, out var formatted, out var error))
        {
            MessageBox.Show(form, error ?? "JSONを整形できません。", "JSON整形", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var caret = editor.CurrentPosition;
        editor.BeginUndoAction();
        try
        {
            editor.SelectAll();
            editor.ReplaceSelection(formatted);
            editor.GotoPosition(Math.Min(caret, editor.TextLength));
        }
        finally { editor.EndUndoAction(); }
    }

    private static void InstallHelpVersion(MenuStrip menu)
    {
        var help = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text.Contains("ヘルプ", StringComparison.Ordinal));
        if (help is null) return;
        ReplaceMenuItem(help, item => item.Text.Contains("バージョン情報", StringComparison.Ordinal),
            new ToolStripMenuItem("バージョン情報", null, (_, _) => MessageBox.Show("vi_text_editor v0.1.16\nNon-overflow X/Y coordinate overlay", "バージョン情報", MessageBoxButtons.OK, MessageBoxIcon.Information)));
        help.DropDownItems.Add(new ToolStripMenuItem("v0.1.16 ワークスペース操作", null, (_, _) => MessageBox.Show(
            "Ctrl+T: 新しいタブ\nCtrl+W: タブを閉じる\nCtrl+Tab: 次のタブ\nCtrl+Shift+Tab: 前のタブ\n\n右下: X=桁 / Y=行（1始まり）\n座標はStatusStripのoverflowに入らない専用表示です。\n拡張子に応じて Markdown / JSON / XML / C# / Python / JavaScript / TypeScript / YAML を強調表示します。\n64MiB以上のテキストはLarge Fileモードでストリーミング表示します。\nJSON整形: Ctrl+Shift+J\nMarkdownプレビュー: Ctrl+Shift+M",
            "ワークスペース操作", MessageBoxButtons.OK, MessageBoxIcon.Information)));
    }

    private static void InstallCoordinateUpdater(MainForm form, EditorWorkspaceForm workspace)
    {
        var editor = GetEditor(form);
        var status = FindControls<StatusStrip>(form).FirstOrDefault();
        var labels = status?.Items.OfType<ToolStripStatusLabel>().ToList();
        var legacyPosition = labels?.LastOrDefault();
        if (editor is null || status is null || labels is null || labels.Count < 5 || legacyPosition is null) return;

        // ToolStrip/StatusStrip can move the last item into overflow when MainForm is embedded in a tab.
        // Do not use a ToolStrip item for coordinates at all. Reserve physical space at the right side
        // of the StatusStrip and place a normal WinForms Label over that reserved area. A normal Control
        // never participates in ToolStrip overflow, so X/Y remains visible regardless of ToolStrip layout.
        foreach (var item in status.Items.OfType<ToolStripItem>().Where(item => item.Name == "CoordinateSpacer").ToArray())
        {
            status.Items.Remove(item);
            item.Dispose();
        }
        foreach (var label in labels) label.Spring = false;

        legacyPosition.Visible = false;
        legacyPosition.Available = false;
        status.SizingGrip = false;
        status.CanOverflow = false;
        var padding = status.Padding;
        status.Padding = new Padding(padding.Left, padding.Top, CoordinateOverlayWidth + 10, padding.Bottom);

        var coordinate = new Label
        {
            Name = "WorkspaceCoordinateOverlay",
            AutoSize = false,
            Text = "X=1  Y=1",
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.ControlText,
            Font = status.Font,
            TabStop = false
        };
        coordinate.AccessibleName = "カーソル座標";
        coordinate.AccessibleDescription = "Xは1始まりの桁位置、Yは1始まりの行番号";
        form.Controls.Add(coordinate);

        void PositionCoordinateOverlay()
        {
            if (form.IsDisposed || form.Disposing || coordinate.IsDisposed || status.IsDisposed) return;
            var height = Math.Max(18, status.Height - 2);
            var left = Math.Max(0, form.ClientSize.Width - CoordinateOverlayWidth - 6);
            coordinate.SetBounds(left, status.Top + 1, CoordinateOverlayWidth, height);
            coordinate.BringToFront();
        }

        string? lastPath = null;
        void RefreshCoordinateAndPath()
        {
            if (form.IsDisposed || form.Disposing || editor.IsDisposed || editor.Disposing || coordinate.IsDisposed) return;
            try
            {
                if (editor.Visible)
                {
                    var caret = Math.Clamp(editor.CurrentPosition, 0, editor.TextLength);
                    var y = editor.LineFromPosition(caret) + 1;
                    var x = editor.GetColumn(caret) + 1;
                    coordinate.Text = $"X={x}  Y={y}";
                    coordinate.Visible = true;
                }
                else
                {
                    coordinate.Text = legacyPosition.Text;
                    coordinate.Visible = !string.IsNullOrWhiteSpace(coordinate.Text);
                }

                PositionCoordinateOverlay();

                var path = GetFilePath(form);
                if (!string.Equals(lastPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    lastPath = path;
                    workspace.NotifyEditorPathChanged(form, path);
                    SyntaxHighlightingService.Apply(editor, path);
                }
            }
            catch (ObjectDisposedException)
            {
                // A child editor can be in the final WinForms disposal phase while a queued UI event remains.
                // Do not let a status/syntax refresh turn a normal tab close into a JIT exception.
            }
        }

        editor.UpdateUI += (_, _) => RefreshCoordinateAndPath();
        editor.MouseUp += (_, _) => RefreshCoordinateAndPath();
        editor.KeyUp += (_, _) => RefreshCoordinateAndPath();
        form.Shown += (_, _) => RefreshCoordinateAndPath();
        form.Resize += (_, _) => PositionCoordinateOverlay();
        status.LocationChanged += (_, _) => PositionCoordinateOverlay();
        status.SizeChanged += (_, _) => PositionCoordinateOverlay();

        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            if (form.IsDisposed || form.Disposing || editor.IsDisposed || editor.Disposing || coordinate.IsDisposed)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }
            RefreshCoordinateAndPath();
        };
        form.FormClosed += (_, _) => { timer.Stop(); timer.Dispose(); };
        timer.Start();
        PositionCoordinateOverlay();
        RefreshCoordinateAndPath();
    }

    private static void ReplaceMenuItem(ToolStripMenuItem parent, Func<ToolStripMenuItem, bool> predicate, ToolStripMenuItem replacement)
    {
        for (var i = 0; i < parent.DropDownItems.Count; i++)
        {
            if (parent.DropDownItems[i] is not ToolStripMenuItem item || !predicate(item)) continue;
            parent.DropDownItems.RemoveAt(i);
            item.Dispose();
            parent.DropDownItems.Insert(i, replacement);
            return;
        }
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control control in root.Controls)
        {
            if (control is T typed) yield return typed;
            if (!control.HasChildren) continue;
            foreach (var child in FindControls<T>(control)) yield return child;
        }
    }
}
