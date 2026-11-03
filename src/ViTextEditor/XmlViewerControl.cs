using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal sealed class XmlViewerControl : UserControl
{
    private readonly TreeView _tree = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripLabel _statusLabel = new();
    private readonly TextBox _command = new();
    private readonly Func<string> _xmlProvider;
    private readonly ViOptions _options = ViOptions.Shared;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 200 };
    private bool _pendingG;
    private char _commandPrefix;
    private string? _lastSearch;
    private bool _lastSearchForward = true;
    private bool _lastSearchIgnoreCase;
    private int _elementCount;
    private string? _parseError;

    public XmlViewerControl(string sourceName, Func<string> xmlProvider)
    {
        _xmlProvider = xmlProvider;
        Dock = DockStyle.Fill;

        _statusLabel.Alignment = ToolStripItemAlignment.Right;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Items.Add(new ToolStripButton("更新", null, (_, _) => RefreshView()));
        _toolbar.Items.Add(new ToolStripButton("すべて展開", null, (_, _) => { _tree.ExpandAll(); UpdateStatus(); }));
        _toolbar.Items.Add(new ToolStripButton("すべて折りたたむ", null, (_, _) => CollapseAll()));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel(sourceName));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("vi: j/k  h/l  gg/G  Ctrl+F/B/D/U  / ? n/N"));
        _toolbar.Items.Add(_statusLabel);
        _toolbar.Dock = DockStyle.Top;

        _command.Dock = DockStyle.Bottom;
        _command.Visible = false;
        _command.BorderStyle = BorderStyle.FixedSingle;
        _command.Font = new Font("Consolas", 10f);
        _command.KeyDown += CommandOnKeyDown;

        _tree.Dock = DockStyle.Fill;
        _tree.BorderStyle = BorderStyle.None;
        _tree.Font = new Font(SelectJapaneseMonospacedFont(), 10f);
        _tree.HideSelection = false;
        _tree.FullRowSelect = true;
        _tree.ShowNodeToolTips = true;
        _tree.AfterSelect += (_, _) => UpdateStatus();
        _tree.KeyPress += TreeOnKeyPress;
        _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
        _tree.DrawNode += TreeOnDrawNode;

        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshView();
        };

        Controls.Add(_tree);
        Controls.Add(_command);
        Controls.Add(_toolbar);
        RefreshView();
    }

    public void FocusViewer() => _tree.Focus();

    internal int NodeCountForSmokeTest => FlattenNodes().Count;
    internal int ElementCountForSmokeTest => _elementCount;
    internal string? ParseErrorForSmokeTest => _parseError;
    internal string SelectedTextForSmokeTest => _tree.SelectedNode?.Text ?? string.Empty;

    public void QueueRefresh()
    {
        if (IsDisposed || Disposing) return;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    public void CancelCommandInput()
    {
        if (_command.Visible) EndCommand();
    }

    public void RefreshView()
    {
        string text;
        try { text = _xmlProvider(); }
        catch { text = string.Empty; }

        var selectedPath = GetSelectedPath();
        var expandedPaths = GetExpandedPaths();
        var result = XmlTreeParser.Parse(text);
        _elementCount = result.ElementCount;
        _parseError = result.ErrorMessage;

        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            if (result.Root is { } root)
            {
                _tree.Nodes.Add(CreateTreeNode(root));
                RestoreExpandedPaths(expandedPaths);
                RestoreSelection(selectedPath);
                if (_tree.SelectedNode is null)
                {
                    _tree.SelectedNode = _tree.Nodes[0];
                    _tree.Nodes[0].Expand();
                }
            }
            else
            {
                var error = new TreeNode(result.ErrorMessage ?? "XMLを解析できません。")
                {
                    ForeColor = Color.Firebrick,
                    ToolTipText = result.ErrorMessage ?? string.Empty
                };
                _tree.Nodes.Add(error);
                _tree.SelectedNode = error;
            }
        }
        finally
        {
            _tree.EndUpdate();
        }

        _pendingG = false;
        UpdateStatus();
    }

    public bool HandleKey(Keys keyData)
    {
        if (_command.Visible) return false;
        var key = keyData & Keys.KeyCode;
        var control = (keyData & Keys.Control) == Keys.Control;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        if ((keyData & Keys.Alt) == Keys.Alt) return false;

        if (control)
        {
            var page = Math.Max(1, _tree.VisibleCount - 1);
            switch (key)
            {
                case Keys.F: MoveVisible(page); return true;
                case Keys.B: MoveVisible(-page); return true;
                case Keys.D: MoveVisible(Math.Max(1, page / 2)); return true;
                case Keys.U: MoveVisible(-Math.Max(1, page / 2)); return true;
            }
            return false;
        }

        if (key == Keys.G)
        {
            if (shift) { _pendingG = false; MoveToLast(); }
            else if (_pendingG) { _pendingG = false; MoveToFirst(); }
            else _pendingG = true;
            return true;
        }

        _pendingG = false;
        switch (key)
        {
            case Keys.J: MoveVisible(1); return true;
            case Keys.K: MoveVisible(-1); return true;
            case Keys.H: CollapseOrParent(); return true;
            case Keys.L: ExpandOrChild(); return true;
            case Keys.N: RepeatSearch(reverse: shift); return true;
        }
        return false;
    }

    private void TreeOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_command.Visible) return;
        if (e.KeyChar is ':' or '/' or '?')
        {
            BeginCommand(e.KeyChar);
            e.Handled = true;
        }
    }

    private void BeginCommand(char prefix)
    {
        _commandPrefix = prefix;
        _command.Text = prefix.ToString();
        _command.Visible = true;
        _command.BringToFront();
        _command.Focus();
        _command.SelectionStart = 1;
    }

    private void CommandOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { EndCommand(); Consume(e); return; }
        if (e.KeyCode == Keys.Back && _command.SelectionStart <= 1 && _command.SelectionLength == 0) { Consume(e); return; }
        if (e.KeyCode != Keys.Enter) return;

        var value = _command.Text.Length > 1 ? _command.Text[1..] : string.Empty;
        if (_commandPrefix == ':')
        {
            if (_options.TryExecuteSet(value, out var message)) _statusLabel.Text = message ?? string.Empty;
            else _statusLabel.Text = $"E492: Not an editor command: {value}";
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            Search(value, _commandPrefix == '/');
        }
        EndCommand(keepStatus: true);
        Consume(e);
    }

    private void EndCommand(bool keepStatus = false)
    {
        _command.Visible = false;
        _command.Text = string.Empty;
        _tree.Focus();
        if (!keepStatus) UpdateStatus();
    }

    private void Search(string query, bool forward)
    {
        var forceIgnore = query.Contains("\\c", StringComparison.Ordinal);
        var forceCase = query.Contains("\\C", StringComparison.Ordinal);
        query = query.Replace("\\c", string.Empty, StringComparison.Ordinal)
                     .Replace("\\C", string.Empty, StringComparison.Ordinal);
        var ignoreCase = forceCase ? false : forceIgnore || _options.IgnoreCase;
        if (!Find(query, forward, ignoreCase))
        {
            _statusLabel.Text = $"Pattern not found: {query}";
            return;
        }
        _lastSearch = query;
        _lastSearchForward = forward;
        _lastSearchIgnoreCase = ignoreCase;
    }

    private void RepeatSearch(bool reverse)
    {
        if (string.IsNullOrEmpty(_lastSearch)) return;
        var forward = reverse ? !_lastSearchForward : _lastSearchForward;
        if (!Find(_lastSearch, forward, _lastSearchIgnoreCase))
            _statusLabel.Text = $"Pattern not found: {_lastSearch}";
    }

    private bool Find(string query, bool forward, bool ignoreCase)
    {
        var nodes = FlattenNodes();
        if (nodes.Count == 0) return false;
        var start = Math.Max(0, nodes.IndexOf(_tree.SelectedNode!));
        var comparison = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        for (var step = 1; step <= nodes.Count; step++)
        {
            var index = forward
                ? (start + step) % nodes.Count
                : (start - step + nodes.Count * 2) % nodes.Count;
            if (!nodes[index].Text.Contains(query, comparison)) continue;
            ExpandAncestors(nodes[index]);
            _tree.SelectedNode = nodes[index];
            nodes[index].EnsureVisible();
            return true;
        }
        return false;
    }

    private void MoveVisible(int delta)
    {
        var node = _tree.SelectedNode;
        if (node is null) return;
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            var next = delta > 0 ? node.NextVisibleNode : node.PrevVisibleNode;
            if (next is null) break;
            node = next;
        }
        _tree.SelectedNode = node;
        node.EnsureVisible();
    }

    private void CollapseOrParent()
    {
        var node = _tree.SelectedNode;
        if (node is null) return;
        if (node.IsExpanded && node.Nodes.Count > 0) node.Collapse();
        else if (node.Parent is not null) _tree.SelectedNode = node.Parent;
    }

    private void ExpandOrChild()
    {
        var node = _tree.SelectedNode;
        if (node is null || node.Nodes.Count == 0) return;
        if (!node.IsExpanded) node.Expand();
        else _tree.SelectedNode = node.Nodes[0];
    }

    private void MoveToFirst()
    {
        if (_tree.Nodes.Count == 0) return;
        _tree.SelectedNode = _tree.Nodes[0];
        _tree.SelectedNode.EnsureVisible();
    }

    private void MoveToLast()
    {
        var nodes = FlattenNodes();
        if (nodes.Count == 0) return;
        var last = nodes[^1];
        ExpandAncestors(last);
        _tree.SelectedNode = last;
        last.EnsureVisible();
    }

    private void CollapseAll()
    {
        _tree.CollapseAll();
        if (_tree.Nodes.Count > 0)
        {
            _tree.Nodes[0].Expand();
            _tree.SelectedNode = _tree.Nodes[0];
        }
        UpdateStatus();
    }

    private TreeNode CreateTreeNode(XmlTreeNode source)
    {
        var node = new TreeNode(source.Label) { Tag = source.Kind, ToolTipText = source.Label };
        foreach (var child in source.Children) node.Nodes.Add(CreateTreeNode(child));
        return node;
    }

    private void TreeOnDrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var selected = (e.State & TreeNodeStates.Selected) != 0;
        var back = selected ? SystemColors.Highlight : _tree.BackColor;
        var fore = selected ? SystemColors.HighlightText : ColorFor(e.Node.Tag as XmlTreeNodeKind?);
        using var brush = new SolidBrush(back);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, _tree.Font, e.Bounds, fore,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
    }

    private static Color ColorFor(XmlTreeNodeKind? kind) => kind switch
    {
        XmlTreeNodeKind.Element => Color.FromArgb(0, 70, 140),
        XmlTreeNodeKind.Attribute => Color.FromArgb(150, 60, 0),
        XmlTreeNodeKind.Comment => Color.ForestGreen,
        XmlTreeNodeKind.CData => Color.Purple,
        XmlTreeNodeKind.Declaration or XmlTreeNodeKind.ProcessingInstruction => Color.Gray,
        _ => SystemColors.ControlText
    };

    private List<TreeNode> FlattenNodes()
    {
        var result = new List<TreeNode>();
        void Add(TreeNode node)
        {
            result.Add(node);
            foreach (TreeNode child in node.Nodes) Add(child);
        }
        foreach (TreeNode root in _tree.Nodes) Add(root);
        return result;
    }

    private int[] GetSelectedPath() => GetNodePath(_tree.SelectedNode);

    private HashSet<string> GetExpandedPaths() =>
        FlattenNodes().Where(node => node.IsExpanded)
            .Select(node => string.Join(".", GetNodePath(node)))
            .ToHashSet(StringComparer.Ordinal);

    private static int[] GetNodePath(TreeNode? node)
    {
        if (node is null) return [];
        var indexes = new Stack<int>();
        for (var current = node; current is not null; current = current.Parent) indexes.Push(current.Index);
        return indexes.ToArray();
    }

    private void RestoreExpandedPaths(HashSet<string> paths)
    {
        foreach (var node in FlattenNodes())
            if (paths.Contains(string.Join(".", GetNodePath(node)))) node.Expand();
    }

    private void RestoreSelection(int[] path)
    {
        TreeNodeCollection nodes = _tree.Nodes;
        TreeNode? selected = null;
        foreach (var index in path)
        {
            if (index < 0 || index >= nodes.Count) break;
            selected = nodes[index];
            nodes = selected.Nodes;
        }
        if (selected is not null) _tree.SelectedNode = selected;
    }

    private static void ExpandAncestors(TreeNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent) parent.Expand();
    }

    private void UpdateStatus()
    {
        if (_parseError is not null)
        {
            _statusLabel.Text = _parseError;
            return;
        }
        var selected = _tree.SelectedNode;
        _statusLabel.Text = $"{_elementCount:N0} 要素  深さ {(selected?.Level ?? 0):N0}  " +
                            (_options.IgnoreCase ? "ignorecase" : "case-sensitive");
    }

    private static void Consume(KeyEventArgs e) { e.Handled = true; e.SuppressKeyPress = true; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private static string SelectJapaneseMonospacedFont()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        var names = fonts.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { "BIZ UDGothic", "MS Gothic", "Yu Gothic UI", "Consolas" })
            if (names.Contains(candidate)) return candidate;
        return FontFamily.GenericMonospace.Name;
    }
}
