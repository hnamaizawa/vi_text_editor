using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal sealed class EditorWorkspaceForm : Form
{
    private readonly TabControl _tabs = new();
    private readonly RecentFileStore _recentFiles = new();
    private readonly Dictionary<TabPage, WorkspaceTab> _sessions = new();
    private readonly HashSet<MainForm> _deferredClosePass = [];
    private bool _closingWorkspace;

    public EditorWorkspaceForm(IEnumerable<string>? startupPaths = null)
    {
        Text = "vi_text_editor";
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        _tabs.Dock = DockStyle.Fill;
        _tabs.Padding = new Point(16, 5);
        _tabs.SelectedIndexChanged += (_, _) => UpdateWorkspaceTitle();
        Controls.Add(_tabs);

        EnableFileDrop(this);

        FormClosing += WorkspaceOnFormClosing;

        var paths = startupPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray() ?? [];
        if (paths.Length == 0)
        {
            AddBlankEditorTab(select: true);
        }
        else
        {
            foreach (var path in paths) OpenPath(path, select: true);
            if (_tabs.TabCount == 0) AddBlankEditorTab(select: true);
        }
    }

    public RecentFileStore RecentFiles => _recentFiles;

    internal bool IsPathOpen(string path)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch { return false; }

        return _sessions.Values.Any(session =>
            !string.IsNullOrWhiteSpace(session.FilePath) &&
            string.Equals(Path.GetFullPath(session.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
    }

    public void NewTab() => AddBlankEditorTab(select: true);

    public void OpenFileFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "テキスト/データファイル|*.txt;*.log;*.csv;*.md;*.markdown;*.json;*.xml;*.cs;*.py;*.js;*.ts;*.yaml;*.yml|すべてのファイル|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (var file in dialog.FileNames) OpenPath(file, select: true);
    }

    public void OpenPath(string path, bool select = true)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch { return; }
        if (!File.Exists(fullPath))
        {
            MessageBox.Show(this, $"ファイルが見つかりません。\n{fullPath}", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var existing = _sessions.FirstOrDefault(pair => string.Equals(pair.Value.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing.Key is not null)
        {
            if (select) _tabs.SelectedTab = existing.Key;
            return;
        }

        var length = new FileInfo(fullPath).Length;
        if (LargeFilePolicy.ShouldUseLargeFileMode(length))
        {
            AddLargeFileTab(fullPath, select);
            return;
        }
        AddEditorTab(fullPath, select);
    }

    public void OpenPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths) OpenPath(path, select: true);
    }

    public void ActivateFromExternalRequest()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
        BringToFront();
        FocusSelectedTabContent();
    }

    private void EnableFileDrop(Control control)
    {
        if (control is WebBrowser) return;

        try { control.AllowDrop = true; }
        catch (InvalidOperationException) { return; }

        control.DragEnter += FileDropOnDragEnter;
        control.DragDrop += FileDropOnDragDrop;
        control.ControlAdded += (_, e) => EnableFileDrop(e.Control);
        foreach (Control child in control.Controls) EnableFileDrop(child);
    }

    private static void FileDropOnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void FileDropOnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        OpenPaths(paths.Where(File.Exists));
        ActivateFromExternalRequest();
    }

    public void OpenMarkdownPreview(MainForm source)
    {
        var editor = MainFormWorkspaceBridge.GetEditor(source);
        if (editor is null) return;
        var sourcePath = MainFormWorkspaceBridge.GetFilePath(source);
        var sourceName = sourcePath is null ? "無題" : Path.GetFileName(sourcePath);
        var page = new TabPage($"{sourceName} [Markdown]");
        var preview = new MarkdownPreviewControl(sourceName, () => source.IsDisposed ? string.Empty : editor.Text);
        page.Controls.Add(preview);
        _tabs.TabPages.Add(page);
        _sessions[page] = new WorkspaceTab(WorkspaceTabKind.MarkdownPreview, null, null, preview, null);
        _tabs.SelectedTab = page;
        preview.FocusViewer();
        UpdateWorkspaceTitle();
    }

    public void CloseCurrentTab()
    {
        if (_tabs.SelectedTab is { } page) CloseTab(page);
    }

    public void SelectNextTab(int delta)
    {
        if (_tabs.TabCount < 2) return;

        // A viewer-owned command/search box must not keep focus when moving away
        // from its tab. Cancel transient input first, then move focus explicitly to
        // the newly selected tab's main control.
        if (_tabs.SelectedTab is { } current &&
            _sessions.TryGetValue(current, out var currentSession))
        {
            currentSession.MarkdownPreview?.CancelCommandInput();
        }

        var next = (_tabs.SelectedIndex + delta) % _tabs.TabCount;
        if (next < 0) next += _tabs.TabCount;
        _tabs.SelectedIndex = next;
        FocusSelectedTabContent();
    }

    public void ExitApplication() => Close();

    public void NotifyEditorPathChanged(MainForm editorForm, string? path)
    {
        var entry = _sessions.FirstOrDefault(pair => ReferenceEquals(pair.Value.EditorForm, editorForm));
        if (entry.Key is null) return;
        var current = entry.Value;
        current.FilePath = path;
        if (!string.IsNullOrWhiteSpace(path)) _recentFiles.Add(path);
        UpdateTabTitle(entry.Key, current);
    }

    private void AddBlankEditorTab(bool select) => AddEditorTab(null, select);

    private void AddEditorTab(string? path, bool select)
    {
        var page = new TabPage("無題");
        var child = new MainForm
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
            StartPosition = FormStartPosition.Manual
        };
        page.Controls.Add(child);
        _tabs.TabPages.Add(page);

        var session = new WorkspaceTab(WorkspaceTabKind.Editor, path, child, null, null);
        _sessions[page] = session;
        MainFormWorkspaceBridge.Install(child, this);
        ConfigureEmbeddedEditor(child);
        child.FormClosing += (_, e) => DeferEmbeddedEditorClose(child, e);
        child.FormClosed += (_, _) =>
        {
            session.ZoomFilter?.Dispose();
            session.ZoomFilter = null;
            RemoveClosedEditorTab(page);
        };
        child.TextChanged += (_, _) => UpdateTabTitle(page, session);
        child.Show();
        ImeSupport.Configure(child);

        if (MainFormWorkspaceBridge.GetEditor(child) is { } editor)
            session.ZoomFilter = SmoothEditorZoom.Attach(editor);

        if (path is not null)
        {
            if (!MainFormWorkspaceBridge.OpenPath(child, path))
            {
                session.ZoomFilter?.Dispose();
                child.Dispose();
                _sessions.Remove(page);
                _tabs.TabPages.Remove(page);
                page.Dispose();
                if (_tabs.TabCount == 0) AddBlankEditorTab(true);
                return;
            }
            session.FilePath = Path.GetFullPath(path);
            _recentFiles.Add(session.FilePath);
        }

        UpdateTabTitle(page, session);
        if (select) _tabs.SelectedTab = page;
        UpdateWorkspaceTitle();
    }

    private static void ConfigureEmbeddedEditor(MainForm child)
    {
        var menu = child.MainMenuStrip;
        if (menu is null) return;

        // Normal startup is editable. Reference mode remains available as an
        // explicit opt-in safety mode from the Mode menu.
        var mode = menu.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Contains("モード", StringComparison.Ordinal));
        var reference = mode?.DropDownItems.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Contains("参照モード", StringComparison.Ordinal));
        if (reference?.Checked == true) reference.Checked = false;

        // Per-tab ToolStrip shortcut processing can resolve a hidden/inactive tab.
        // Show the shortcut text, but let the workspace route the actual key to the
        // selected tab exactly once.
        var tools = menu.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Contains("ツール", StringComparison.Ordinal));
        if (tools is not null)
        {
            foreach (var item in tools.DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (item.Text.StartsWith("JSONを整形", StringComparison.Ordinal))
                {
                    item.ShortcutKeys = Keys.None;
                    item.ShortcutKeyDisplayString = "Ctrl+Shift+J";
                }
                else if (item.Text.StartsWith("Markdownプレビュー", StringComparison.Ordinal))
                {
                    item.ShortcutKeys = Keys.None;
                    item.ShortcutKeyDisplayString = "Ctrl+Shift+M";
                }
            }
        }

        var help = menu.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Contains("ヘルプ", StringComparison.Ordinal));
        if (help is not null)
        {
            ReplaceHelpItem(help, item => item.Text.Contains("バージョン情報", StringComparison.Ordinal),
                new ToolStripMenuItem("バージョン情報", null, (_, _) => MessageBox.Show(
                    child,
                    "vi_text_editor v0.1.25\nSingle instance / drag and drop",
                    "バージョン情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)));
            ReplaceHelpItem(help, item => item.Text.Contains("ワークスペース操作", StringComparison.Ordinal),
                new ToolStripMenuItem("v0.1.25 ワークスペース操作", null, (_, _) => MessageBox.Show(
                    child,
                    "Ctrl+Shift+J: JSON整形\nCtrl+Shift+M: Markdownプレビュー\nCtrl+Tab / Ctrl+Shift+Tab: タブ切替\nCtrl+PageDown / Ctrl+PageUp: タブ切替\n\nMarkdown vi: j/k, Ctrl+F/B/D/U, gg/G, / ? n/N\nMarkdown COMMAND: :set ic / :set noic / :set ic?\nCtrl+マウスホイール: デバウンスされた拡大縮小\n\nWindows: 二重起動せず、Shell起動／ドラッグ＆ドロップしたファイルを既存ウィンドウの新規タブで開く",
                    "ワークスペース操作",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)));
        }
    }

    private static void ReplaceHelpItem(ToolStripMenuItem parent, Func<ToolStripMenuItem, bool> predicate, ToolStripMenuItem replacement)
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

    private void DeferEmbeddedEditorClose(MainForm editor, FormClosingEventArgs e)
    {
        if (_closingWorkspace || e.Cancel || editor.IsDisposed) return;

        // MainForm.Close() can be invoked from inside the command-line KeyDown handler (:q/:q!/:wq/:x).
        // Disposing the embedded form synchronously would invalidate its Scintilla control before that
        // KeyDown handler finishes and its final UpdateStatus() call runs. Cancel the first close and
        // perform the actual close on the next UI message instead.
        if (_deferredClosePass.Remove(editor)) return;

        e.Cancel = true;
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || Disposing || editor.IsDisposed) return;
            _deferredClosePass.Add(editor);
            MainFormWorkspaceBridge.AllowDeferredClose(editor);
            editor.Close();
        }));
    }

    private void AddLargeFileTab(string path, bool select)
    {
        var page = new TabPage(Path.GetFileName(path) + " [LARGE]");
        var viewer = new LargeTextViewerControl();
        page.Controls.Add(viewer);
        _tabs.TabPages.Add(page);
        var session = new WorkspaceTab(WorkspaceTabKind.LargeFile, path, null, null, viewer);
        _sessions[page] = session;

        Cursor = Cursors.WaitCursor;
        try
        {
            if (!viewer.LoadFile(path, out var error))
            {
                MessageBox.Show(this, error ?? "大容量ファイルを開けません。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _sessions.Remove(page);
                _tabs.TabPages.Remove(page);
                page.Dispose();
                return;
            }
        }
        finally { Cursor = Cursors.Default; }

        _recentFiles.Add(path);
        if (select) _tabs.SelectedTab = page;
        viewer.FocusViewer();
        UpdateWorkspaceTitle();
    }

    private void CloseTab(TabPage page)
    {
        if (!_sessions.TryGetValue(page, out var session)) return;
        if (session.EditorForm is { } editor)
        {
            editor.Close();
            return;
        }

        session.ZoomFilter?.Dispose();
        session.LargeViewer?.CloseFile();
        _sessions.Remove(page);
        _tabs.TabPages.Remove(page);
        page.Dispose();
        if (_tabs.TabCount == 0 && !_closingWorkspace) AddBlankEditorTab(true);
        UpdateWorkspaceTitle();
    }

    private void RemoveClosedEditorTab(TabPage page)
    {
        if (!_sessions.Remove(page)) return;
        _tabs.TabPages.Remove(page);
        page.Dispose();
        if (_tabs.TabCount == 0 && !_closingWorkspace) AddBlankEditorTab(true);
        UpdateWorkspaceTitle();
    }

    private void UpdateTabTitle(TabPage page, WorkspaceTab session)
    {
        if (session.EditorForm is { } form)
        {
            var title = form.Text;
            const string suffix = " - vi_text_editor";
            if (title.EndsWith(suffix, StringComparison.Ordinal)) title = title[..^suffix.Length];
            page.Text = title.Length > 32 ? title[..29] + "..." : title;
            var path = MainFormWorkspaceBridge.GetFilePath(form);
            if (!string.Equals(path, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                session.FilePath = path;
                if (!string.IsNullOrWhiteSpace(path)) _recentFiles.Add(path);
            }
        }
    }

    private void UpdateWorkspaceTitle()
    {
        var selected = _tabs.SelectedTab;
        Text = selected is null ? "vi_text_editor" : $"{selected.Text} - vi_text_editor workspace";
    }

    private void FocusSelectedTabContent()
    {
        if (_tabs.SelectedTab is not { } selected || !_sessions.TryGetValue(selected, out var session))
        {
            _tabs.Focus();
            return;
        }

        if (session.EditorForm is { IsDisposed: false } form && MainFormWorkspaceBridge.GetEditor(form) is { IsDisposed: false } editor)
        {
            editor.Focus();
            return;
        }
        if (session.MarkdownPreview is { IsDisposed: false } preview)
        {
            preview.FocusViewer();
            return;
        }
        if (session.LargeViewer is { IsDisposed: false } large)
        {
            large.FocusViewer();
            return;
        }
        _tabs.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.T)) { NewTab(); return true; }
        if (keyData == (Keys.Control | Keys.W)) { CloseCurrentTab(); return true; }

        // Workspace tab navigation has higher priority than any embedded viewer.
        // In particular, WebBrowser may otherwise consume Ctrl+PageUp/PageDown
        // after the user scrolls the Markdown preview.
        if (keyData == (Keys.Control | Keys.PageDown)) { SelectNextTab(1); return true; }
        if (keyData == (Keys.Control | Keys.PageUp)) { SelectNextTab(-1); return true; }
        if (keyData == (Keys.Control | Keys.Tab)) { SelectNextTab(1); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)) { SelectNextTab(-1); return true; }

        // Route workspace-wide tool shortcuts to the selected editor only. This avoids
        // hidden tab MenuStrip shortcut collisions.
        if (keyData == (Keys.Control | Keys.Shift | Keys.J) && InvokeActiveEditorTool("JSONを整形")) return true;
        if (keyData == (Keys.Control | Keys.Shift | Keys.M) && InvokeActiveEditorTool("Markdownプレビュー")) return true;

        if (_tabs.SelectedTab is { } selected &&
            _sessions.TryGetValue(selected, out var session) &&
            session.MarkdownPreview is { } preview &&
            preview.HandleKey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool InvokeActiveEditorTool(string startsWith)
    {
        if (_tabs.SelectedTab is not { } selected ||
            !_sessions.TryGetValue(selected, out var session) ||
            session.EditorForm is not { } form ||
            form.IsDisposed)
        {
            return false;
        }

        var tools = form.MainMenuStrip?.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.Contains("ツール", StringComparison.Ordinal));
        var command = tools?.DropDownItems.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text.StartsWith(startsWith, StringComparison.Ordinal));
        if (command is null) return false;
        command.PerformClick();
        return true;
    }

    private void WorkspaceOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closingWorkspace) return;
        _closingWorkspace = true;
        foreach (var form in _sessions.Values.Select(s => s.EditorForm).Where(f => f is not null).Cast<MainForm>().ToArray())
        {
            if (form.IsDisposed) continue;
            form.Close();
            if (!form.IsDisposed)
            {
                e.Cancel = true;
                _closingWorkspace = false;
                return;
            }
        }
    }

    private enum WorkspaceTabKind { Editor, LargeFile, MarkdownPreview }

    private sealed class WorkspaceTab(
        WorkspaceTabKind kind,
        string? filePath,
        MainForm? editorForm,
        MarkdownPreviewControl? markdownPreview,
        LargeTextViewerControl? largeViewer)
    {
        public WorkspaceTabKind Kind { get; } = kind;
        public string? FilePath { get; set; } = filePath;
        public MainForm? EditorForm { get; } = editorForm;
        public MarkdownPreviewControl? MarkdownPreview { get; } = markdownPreview;
        public LargeTextViewerControl? LargeViewer { get; } = largeViewer;
        public IDisposable? ZoomFilter { get; set; }
    }
}
