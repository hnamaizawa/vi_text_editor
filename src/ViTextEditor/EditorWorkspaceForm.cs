using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal sealed class EditorWorkspaceForm : Form
{
    private readonly TabControl _tabs = new();
    private readonly EditorSettingsStore _settingsStore;
    private readonly WorkspaceStateStore _workspaceStateStore;
    private readonly RecentFileStore _recentFiles;
    private readonly Dictionary<TabPage, WorkspaceTab> _sessions = new();
    private readonly HashSet<MainForm> _deferredClosePass = [];
    private bool _closingWorkspace;
    private TabPage? _draggedTab;
    private Point _tabDragStart;
    private EditorSettings _settings;

    public EditorWorkspaceForm(IEnumerable<string>? startupPaths = null, bool restorePreviousWorkspace = true)
    {
        _settingsStore = new EditorSettingsStore();
        _workspaceStateStore = new WorkspaceStateStore();
        _settings = _settingsStore.Load();
        var previousWorkspace = restorePreviousWorkspace ? _workspaceStateStore.Load() : WorkspaceState.Empty;
        _recentFiles = new RecentFileStore(_settings.RecentFileLimit);
        Text = "vi_text_editor";
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        RestoreWindowPlacement(previousWorkspace);

        _tabs.Dock = DockStyle.Fill;
        _tabs.Padding = new Point(16, 5);
        _tabs.SelectedIndexChanged += (_, _) => UpdateWorkspaceTitle();
        _tabs.MouseDown += TabsOnMouseDown;
        _tabs.MouseMove += TabsOnMouseMove;
        _tabs.MouseUp += (_, _) => EndTabDrag();
        _tabs.MouseLeave += (_, _) => EndTabDrag();
        Controls.Add(_tabs);

        EnableFileDrop(this);

        FormClosing += WorkspaceOnFormClosing;

        var requestedPaths = startupPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray() ?? [];
        var paths = BuildStartupPaths(
            previousWorkspace.OpenFiles,
            requestedPaths,
            _settings.RestoreOpenFilesOnStartup == true);
        if (paths.Length == 0)
        {
            AddBlankEditorTab(select: true);
        }
        else
        {
            foreach (var path in paths) OpenPath(path, select: false);
            if (_tabs.TabCount == 0) AddBlankEditorTab(select: true);
            else SelectRestoredTab(
                _settings.RestoreOpenFilesOnStartup == true ? previousWorkspace.SelectedFile : null,
                requestedPaths);
        }
    }

    internal static string[] BuildStartupPaths(
        IEnumerable<string> previousPaths,
        IEnumerable<string> requestedPaths,
        bool restoreOpenFiles)
    {
        var restoredPaths = restoreOpenFiles ? previousPaths : Enumerable.Empty<string>();
        return restoredPaths
            .Concat(requestedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RestoreWindowPlacement(WorkspaceState state)
    {
        if (state.Width < 400 || state.Height < 300) return;

        var bounds = new Rectangle(state.Left, state.Top, state.Width, state.Height);
        if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds))) return;

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        if (state.Maximized) WindowState = FormWindowState.Maximized;
    }

    private void SelectRestoredTab(string? previousSelection, IReadOnlyList<string> requestedPaths)
    {
        var selection = requestedPaths.LastOrDefault() ?? previousSelection;
        if (!string.IsNullOrWhiteSpace(selection))
        {
            string fullSelection;
            try { fullSelection = Path.GetFullPath(selection); }
            catch { fullSelection = string.Empty; }
            var page = _sessions.FirstOrDefault(pair =>
                !string.IsNullOrWhiteSpace(pair.Value.FilePath) &&
                string.Equals(Path.GetFullPath(pair.Value.FilePath), fullSelection, StringComparison.OrdinalIgnoreCase)).Key;
            if (page is not null)
            {
                _tabs.SelectedTab = page;
                return;
            }
        }

        if (_tabs.TabCount > 0) _tabs.SelectedIndex = 0;
    }

    public RecentFileStore RecentFiles => _recentFiles;

    public void ShowSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "設定",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(520, 292)
        };
        var recentLabel = new Label { Text = "最近使ったファイルの表示件数:", AutoSize = true, Location = new Point(18, 24) };
        var recentLimit = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 100,
            Value = _settings.RecentFileLimit,
            Width = 80,
            Location = new Point(290, 20)
        };
        var fullPath = new CheckBox
        {
            Text = "タイトルバーに現在のファイルのフルパスを表示する",
            AutoSize = true,
            Checked = _settings.ShowFullPathInTitle,
            Location = new Point(18, 62)
        };
        var restoreOpenFiles = new CheckBox
        {
            Text = "起動時に前回開いていたファイルを自動的に開く",
            AutoSize = true,
            Checked = _settings.RestoreOpenFilesOnStartup == true,
            Location = new Point(18, 96)
        };
        var restartNote = new Label
        {
            Text = "※ 次回起動時から適用されます",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(36, 122)
        };
        var csvExpandAllColumns = new CheckBox
        {
            Text = "CSVビューアですべての列名とデータを省略せず表示する",
            AutoSize = true,
            Checked = _settings.CsvExpandAllColumnText,
            Location = new Point(18, 158)
        };
        var csvNote = new Label
        {
            Text = "※ 大きなCSVでは表示更新に時間がかかる場合があります",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(36, 184)
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(92, 32), Margin = new Padding(8, 0, 0, 0) };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Size = new Size(92, 32), Margin = new Padding(8, 0, 0, 0) };
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 14, 18, 10)
        };
        buttonPanel.Controls.AddRange([cancel, ok]);
        dialog.Controls.AddRange([recentLabel, recentLimit, fullPath, restoreOpenFiles, restartNote, csvExpandAllColumns, csvNote, buttonPanel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _settings = new EditorSettings(
            (int)recentLimit.Value,
            fullPath.Checked,
            restoreOpenFiles.Checked,
            csvExpandAllColumns.Checked).Normalize();
        _settingsStore.Save(_settings);
        _recentFiles.SetCapacity(_settings.RecentFileLimit);
        foreach (var viewer in _sessions.Values.Select(session => session.CsvViewer).Where(viewer => viewer is { IsDisposed: false }))
            viewer!.SetExpandAllColumnText(_settings.CsvExpandAllColumnText);
        UpdateWorkspaceTitle();
    }

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

        var replaceableBlank = FindReplaceableInitialBlankTab();

        var length = new FileInfo(fullPath).Length;
        if (LargeFilePolicy.ShouldUseLargeFileMode(length))
        {
            AddLargeFileTab(fullPath, select);
            if (IsPathOpen(fullPath)) RemoveReplaceableBlankTab(replaceableBlank);
            return;
        }
        AddEditorTab(fullPath, select);
        if (IsPathOpen(fullPath)) RemoveReplaceableBlankTab(replaceableBlank);
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
        control.ControlAdded += (_, e) =>
        {
            if (e.Control is { } child) EnableFileDrop(child);
        };
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

    private TabPage? FindReplaceableInitialBlankTab()
    {
        if (_tabs.TabCount != 1) return null;
        var page = _tabs.TabPages[0];
        if (!_sessions.TryGetValue(page, out var session) ||
            session.Kind != WorkspaceTabKind.Editor ||
            session.FilePath is not null ||
            session.EditorForm is not { IsDisposed: false } editor ||
            editor.HasUnsavedChanges)
        {
            return null;
        }

        var textEditor = MainFormWorkspaceBridge.GetEditor(editor);
        return textEditor is { TextLength: 0 } ? page : null;
    }

    private void RemoveReplaceableBlankTab(TabPage? page)
    {
        if (page is null || !_sessions.Remove(page, out var session)) return;
        _tabs.TabPages.Remove(page);
        session.ZoomFilter?.Dispose();
        session.EditorForm?.Dispose();
        page.Dispose();
        if (_tabs.TabCount > 0 && (_tabs.SelectedIndex < 0 || _tabs.SelectedIndex >= _tabs.TabCount))
            _tabs.SelectedIndex = 0;
        UpdateWorkspaceTitle();
    }

    private void TabsOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var index = TabIndexAt(e.Location);
        if (index < 0) return;
        _draggedTab = _tabs.TabPages[index];
        _tabDragStart = e.Location;
    }

    private void TabsOnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_draggedTab is null || e.Button != MouseButtons.Left) return;
        var dragSize = SystemInformation.DragSize;
        if (Math.Abs(e.X - _tabDragStart.X) <= dragSize.Width / 2 &&
            Math.Abs(e.Y - _tabDragStart.Y) <= dragSize.Height / 2)
        {
            return;
        }

        var targetIndex = TabIndexAt(e.Location);
        if (targetIndex < 0) return;
        var sourceIndex = _tabs.TabPages.IndexOf(_draggedTab);
        if (sourceIndex < 0 || sourceIndex == targetIndex) return;
        MoveTab(sourceIndex, targetIndex);
        _tabDragStart = e.Location;
        _tabs.Cursor = Cursors.SizeWE;
    }

    private void EndTabDrag()
    {
        _draggedTab = null;
        _tabs.Cursor = Cursors.Default;
    }

    private int TabIndexAt(Point location)
    {
        for (var index = 0; index < _tabs.TabCount; index++)
        {
            if (_tabs.GetTabRect(index).Contains(location)) return index;
        }
        return -1;
    }

    private void MoveTab(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _tabs.TabCount ||
            targetIndex < 0 || targetIndex >= _tabs.TabCount ||
            sourceIndex == targetIndex)
        {
            return;
        }

        var page = _tabs.TabPages[sourceIndex];
        _tabs.TabPages.RemoveAt(sourceIndex);
        _tabs.TabPages.Insert(targetIndex, page);
        _tabs.SelectedTab = page;
        UpdateWorkspaceTitle();
    }

    internal int TabCountForSmokeTest => _tabs.TabCount;

    internal IReadOnlyList<string> TabTitlesForSmokeTest =>
        _tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray();

    internal void MoveTabForSmokeTest(int sourceIndex, int targetIndex) => MoveTab(sourceIndex, targetIndex);

    internal bool TypeIntoSelectedEditorForSmokeTest(string text)
    {
        if (_tabs.SelectedTab is not { } page ||
            !_sessions.TryGetValue(page, out var session) ||
            session.EditorForm is not { IsDisposed: false } form ||
            MainFormWorkspaceBridge.GetEditor(form) is not { IsDisposed: false } editor)
        {
            return false;
        }

        editor.AddText(text);
        return true;
    }

    internal CsvViewerControl? OpenCsvViewerForSmokeTest()
    {
        if (_tabs.SelectedTab is not { } page ||
            !_sessions.TryGetValue(page, out var session) ||
            session.EditorForm is not { IsDisposed: false } form)
        {
            return null;
        }

        OpenCsvViewer(form);
        return _tabs.SelectedTab is { } viewerPage &&
               _sessions.TryGetValue(viewerPage, out var viewerSession)
            ? viewerSession.CsvViewer
            : null;
    }

    public void OpenMarkdownPreview(MainForm source)
    {
        var editor = MainFormWorkspaceBridge.GetEditor(source);
        if (editor is null) return;
        var sourcePath = MainFormWorkspaceBridge.GetFilePath(source);
        var sourceName = sourcePath is null ? "無題" : Path.GetFileName(sourcePath);
        var page = new TabPage($"{sourceName} [Markdown]");
        var preview = new MarkdownPreviewControl(sourceName, () => source.IsDisposed ? string.Empty : editor.Text);
        EventHandler sourceTextChanged = (_, _) => preview.QueueRefresh();
        editor.TextChanged += sourceTextChanged;
        preview.Disposed += (_, _) => editor.TextChanged -= sourceTextChanged;
        page.Controls.Add(preview);
        _tabs.TabPages.Add(page);
        _sessions[page] = new WorkspaceTab(WorkspaceTabKind.MarkdownPreview, null, null, preview, null, null);
        _tabs.SelectedTab = page;
        preview.FocusViewer();
        UpdateWorkspaceTitle();
    }

    public void OpenCsvViewer(MainForm source)
    {
        var editor = MainFormWorkspaceBridge.GetEditor(source);
        if (editor is null) return;
        var sourcePath = MainFormWorkspaceBridge.GetFilePath(source);
        var sourceName = sourcePath is null ? "無題" : Path.GetFileName(sourcePath);
        var page = new TabPage($"{sourceName} [CSV]");
        var viewer = new CsvViewerControl(
            sourceName,
            () => source.IsDisposed ? string.Empty : editor.Text,
            _settings.CsvExpandAllColumnText);
        EventHandler sourceTextChanged = (_, _) => viewer.QueueRefresh();
        editor.TextChanged += sourceTextChanged;
        viewer.Disposed += (_, _) => editor.TextChanged -= sourceTextChanged;
        page.Controls.Add(viewer);
        _tabs.TabPages.Add(page);
        _sessions[page] = new WorkspaceTab(WorkspaceTabKind.CsvViewer, null, null, null, viewer, null);
        _tabs.SelectedTab = page;
        viewer.FocusViewer();
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
            currentSession.CsvViewer?.CancelCommandInput();
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

        var session = new WorkspaceTab(WorkspaceTabKind.Editor, path, child, null, null, null);
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
            session.ZoomFilter = SmoothEditorZoom.Attach(editor, child.RefreshStatus);

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
                else if (item.Text.StartsWith("CSVビューア", StringComparison.Ordinal))
                {
                    item.ShortcutKeys = Keys.None;
                    item.ShortcutKeyDisplayString = "Ctrl+Shift+C";
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
                    "vi_text_editor v0.1.51\nRestored Markdown Enter auto-indent",
                    "バージョン情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)));
            ReplaceHelpItem(help, item => item.Text.Contains("ワークスペース操作", StringComparison.Ordinal),
                new ToolStripMenuItem("v0.1.51 ワークスペース操作", null, (_, _) => MessageBox.Show(
                    child,
                    "Ctrl+Shift+J: JSON整形\nCtrl+Shift+M: Markdownプレビュー\nCtrl+Shift+C: CSVビューア\nCtrl+Tab / Ctrl+Shift+Tab: タブ切替\nCtrl+PageDown / Ctrl+PageUp: タブ切替\nタブ見出しのドラッグ＆ドロップ: 並べ替え\n\nMarkdown vi: j/k, Ctrl+F/B/D/U, gg/G, / ? n/N\nCSV vi: h/j/k/l, gg/G, 0/^/$, Ctrl+F/B/D/U, / ? n/N\nViewer COMMAND: :set ic / :set noic / :set ic?\nCtrl+マウスホイール: デバウンスされた拡大縮小\nURL: http:// / https:// をリンク表示し、シングルクリックで既定ブラウザから開く\n\nWindows: 二重起動せず、Shell起動／ドラッグ＆ドロップしたファイルを既存ウィンドウの新規タブで開く",
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
        var session = new WorkspaceTab(WorkspaceTabKind.LargeFile, path, null, null, null, viewer);
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
        UpdateWorkspaceTitle();
        ExitWorkspaceIfNoTabsRemain();
    }

    private void RemoveClosedEditorTab(TabPage page)
    {
        if (!_sessions.Remove(page)) return;
        _tabs.TabPages.Remove(page);
        page.Dispose();
        UpdateWorkspaceTitle();
        ExitWorkspaceIfNoTabsRemain();
    }

    private void ExitWorkspaceIfNoTabsRemain()
    {
        if (_tabs.TabCount != 0 || _closingWorkspace || IsDisposed || Disposing) return;

        // An embedded editor can finish closing from a deferred :q/:q! callback.
        // Close the workspace on the next UI message to avoid nesting Form.Close calls.
        BeginInvoke(new Action(() =>
        {
            if (_tabs.TabCount == 0 && !_closingWorkspace && !IsDisposed && !Disposing) Close();
        }));
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
        if (ReferenceEquals(_tabs.SelectedTab, page)) UpdateWorkspaceTitle();
    }

    private void UpdateWorkspaceTitle()
    {
        if (_tabs.SelectedTab is not { } page || !_sessions.TryGetValue(page, out var session))
        {
            Text = "vi_text_editor";
            return;
        }

        var displayName = !string.IsNullOrWhiteSpace(session.FilePath)
            ? _settings.ShowFullPathInTitle ? Path.GetFullPath(session.FilePath) : Path.GetFileName(session.FilePath)
            : page.Text;
        var dirtyPrefix = page.Text.StartsWith('*') && !displayName.StartsWith('*') ? "*" : string.Empty;
        Text = $"{dirtyPrefix}{displayName} - vi_text_editor";
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
        if (session.CsvViewer is { IsDisposed: false } csv)
        {
            csv.FocusViewer();
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
        if (keyData == (Keys.Control | Keys.Shift | Keys.C) && InvokeActiveEditorTool("CSVビューア")) return true;

        if (_tabs.SelectedTab is { } selected &&
            _sessions.TryGetValue(selected, out var session) &&
            session.MarkdownPreview is { } preview &&
            preview.HandleKey(keyData))
        {
            return true;
        }

        if (_tabs.SelectedTab is { } csvSelected &&
            _sessions.TryGetValue(csvSelected, out var csvSession) &&
            csvSession.CsvViewer is { } csvViewer &&
            csvViewer.HandleKey(keyData))
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
        var workspaceState = CaptureWorkspaceState();
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
        _workspaceStateStore.Save(workspaceState);
    }

    private WorkspaceState CaptureWorkspaceState()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var openFiles = _tabs.TabPages.Cast<TabPage>()
            .Select(page => _sessions.TryGetValue(page, out var session) ? session.FilePath : null)
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Cast<string>()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selectedFile = _tabs.SelectedTab is { } selected && _sessions.TryGetValue(selected, out var selectedSession)
            ? selectedSession.FilePath
            : null;
        return new WorkspaceState(
            openFiles,
            selectedFile,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            WindowState == FormWindowState.Maximized);
    }

    private enum WorkspaceTabKind { Editor, LargeFile, MarkdownPreview, CsvViewer }

    private sealed class WorkspaceTab(
        WorkspaceTabKind kind,
        string? filePath,
        MainForm? editorForm,
        MarkdownPreviewControl? markdownPreview,
        CsvViewerControl? csvViewer,
        LargeTextViewerControl? largeViewer)
    {
        public WorkspaceTabKind Kind { get; } = kind;
        public string? FilePath { get; set; } = filePath;
        public MainForm? EditorForm { get; } = editorForm;
        public MarkdownPreviewControl? MarkdownPreview { get; } = markdownPreview;
        public CsvViewerControl? CsvViewer { get; } = csvViewer;
        public LargeTextViewerControl? LargeViewer { get; } = largeViewer;
        public IDisposable? ZoomFilter { get; set; }
    }
}
