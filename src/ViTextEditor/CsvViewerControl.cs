using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal sealed class CsvViewerControl : UserControl
{
    private readonly DataGridView _grid = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripLabel _sourceLabel = new();
    private readonly ToolStripLabel _statusLabel = new();
    private readonly TextBox _command = new();
    private readonly Func<string> _csvProvider;
    private readonly ViOptions _options = ViOptions.Shared;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 200 };
    private IReadOnlyList<IReadOnlyList<string>> _rows = [];
    private bool _pendingG;
    private char _commandPrefix;
    private string? _lastSearch;
    private bool _lastSearchForward = true;
    private bool _lastSearchIgnoreCase;
    private bool _expandAllColumnText;

    public CsvViewerControl(string sourceName, Func<string> csvProvider, bool expandAllColumnText = false)
    {
        _csvProvider = csvProvider;
        _expandAllColumnText = expandAllColumnText;
        Dock = DockStyle.Fill;

        _sourceLabel.Text = sourceName;
        _statusLabel.Alignment = ToolStripItemAlignment.Right;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Items.Add(new ToolStripButton("更新", null, (_, _) => RefreshView()));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_sourceLabel);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("vi: h/j/k/l  gg/G  0/^/$  Ctrl+F/B/D/U  / ? n/N"));
        _toolbar.Items.Add(_statusLabel);
        _toolbar.Dock = DockStyle.Top;

        _command.Dock = DockStyle.Bottom;
        _command.Visible = false;
        _command.BorderStyle = BorderStyle.FixedSingle;
        _command.Font = new Font("Consolas", 10f);
        _command.KeyDown += CommandOnKeyDown;

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Font = new Font(SelectJapaneseMonospacedFont(), 10f);
        _grid.RowHeadersWidth = 64;
        _grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.RowPostPaint += GridOnRowPostPaint;
        _grid.CurrentCellChanged += (_, _) => UpdateStatus();
        _grid.KeyPress += GridOnKeyPress;

        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshView();
        };

        Controls.Add(_grid);
        Controls.Add(_command);
        Controls.Add(_toolbar);
        RefreshView();
    }

    public void FocusViewer() => _grid.Focus();

    internal int RowCountForSmokeTest => _grid.RowCount;
    internal int ColumnCountForSmokeTest => _grid.ColumnCount;
    internal Point CurrentCellForSmokeTest => new(CurrentColumn(), CurrentRow());
    internal int ColumnWidthForSmokeTest(int column) => _grid.Columns[column].Width;

    public void SetExpandAllColumnText(bool enabled)
    {
        if (_expandAllColumnText == enabled) return;
        _expandAllColumnText = enabled;
        RefreshView();
    }

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
        try { text = _csvProvider(); }
        catch { text = string.Empty; }

        var table = CsvTableParser.Parse(text);
        _rows = table.Rows;
        var columnCount = Math.Max(1, _rows.Count == 0 ? 1 : _rows.Max(row => row.Count));

        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            _grid.Columns.Clear();
            for (var column = 0; column < columnCount; column++)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = (column + 1).ToString(),
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Width = CalculateColumnWidth(column)
                });
            }

            foreach (var sourceRow in _rows)
            {
                var values = new object[columnCount];
                for (var column = 0; column < sourceRow.Count && column < columnCount; column++)
                    values[column] = sourceRow[column];
                _grid.Rows.Add(values);
            }
        }
        finally
        {
            _grid.ResumeLayout();
        }

        if (_grid.RowCount > 0) _grid.CurrentCell = _grid.Rows[0].Cells[0];
        _pendingG = false;
        _lastSearch = null;
        var delimiterName = table.Delimiter switch { '\t' => "TAB", ';' => ";", _ => "," };
        _statusLabel.Text = $"{_rows.Count:N0} 行 × {columnCount:N0} 列  区切り: {delimiterName}" +
                            (table.Truncated ? "  [表示上限により省略]" : string.Empty);
    }

    public bool HandleKey(Keys keyData)
    {
        if (_command.Visible) return false;
        var key = keyData & Keys.KeyCode;
        var control = (keyData & Keys.Control) == Keys.Control;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        if (alt) return false;

        if (control)
        {
            switch (key)
            {
                case Keys.F: MoveRows(VisibleRowCount()); return true;
                case Keys.B: MoveRows(-VisibleRowCount()); return true;
                case Keys.D: MoveRows(Math.Max(1, VisibleRowCount() / 2)); return true;
                case Keys.U: MoveRows(-Math.Max(1, VisibleRowCount() / 2)); return true;
            }
            return false;
        }

        if (key == Keys.G)
        {
            if (shift) { _pendingG = false; MoveTo(_grid.RowCount - 1, CurrentColumn()); }
            else if (_pendingG) { _pendingG = false; MoveTo(0, CurrentColumn()); }
            else _pendingG = true;
            return true;
        }

        _pendingG = false;
        switch (key)
        {
            case Keys.H: MoveColumns(-1); return true;
            case Keys.J: MoveRows(1); return true;
            case Keys.K: MoveRows(-1); return true;
            case Keys.L: MoveColumns(1); return true;
            case Keys.N: RepeatSearch(reverse: shift); return true;
            case Keys.D0:
            case Keys.NumPad0: MoveTo(CurrentRow(), 0); return true;
            case Keys.D6 when shift: MoveTo(CurrentRow(), 0); return true;
            case Keys.D4 when shift: MoveTo(CurrentRow(), _grid.ColumnCount - 1); return true;
        }
        return false;
    }

    private void GridOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_command.Visible) return;
        if (e.KeyChar is ':' or '/' or '?')
        {
            BeginCommand(e.KeyChar);
            e.Handled = true;
        }
        else if (e.KeyChar == '$')
        {
            MoveTo(CurrentRow(), _grid.ColumnCount - 1);
            e.Handled = true;
        }
        else if (e.KeyChar == '^')
        {
            MoveTo(CurrentRow(), 0);
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
        _grid.Focus();
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
        if (_grid.RowCount == 0 || _grid.ColumnCount == 0) return false;
        var total = _grid.RowCount * _grid.ColumnCount;
        var start = CurrentRow() * _grid.ColumnCount + CurrentColumn();
        var comparison = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        for (var step = 1; step <= total; step++)
        {
            var index = forward ? (start + step) % total : (start - step + total * 2) % total;
            var row = index / _grid.ColumnCount;
            var column = index % _grid.ColumnCount;
            if ((_grid.Rows[row].Cells[column].Value?.ToString() ?? string.Empty).Contains(query, comparison))
            {
                MoveTo(row, column);
                return true;
            }
        }
        return false;
    }

    private void MoveRows(int delta) => MoveTo(CurrentRow() + delta, CurrentColumn());
    private void MoveColumns(int delta) => MoveTo(CurrentRow(), CurrentColumn() + delta);

    private void MoveTo(int row, int column)
    {
        if (_grid.RowCount == 0 || _grid.ColumnCount == 0) return;
        row = Math.Clamp(row, 0, _grid.RowCount - 1);
        column = Math.Clamp(column, 0, _grid.ColumnCount - 1);
        _grid.CurrentCell = _grid.Rows[row].Cells[column];
        UpdateStatus();
    }

    private int CurrentRow() => _grid.CurrentCell?.RowIndex ?? 0;
    private int CurrentColumn() => _grid.CurrentCell?.ColumnIndex ?? 0;
    private int VisibleRowCount() => Math.Max(1, _grid.DisplayedRowCount(includePartialRow: false));

    private int CalculateColumnWidth(int column)
    {
        if (_expandAllColumnText)
        {
            var width = MeasureCellWidth((column + 1).ToString(), _grid.ColumnHeadersDefaultCellStyle.Font ?? _grid.Font);
            foreach (var row in _rows)
            {
                if (column >= row.Count) continue;
                foreach (var line in row[column].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                    width = Math.Max(width, MeasureCellWidth(line, _grid.Font));
            }
            return Math.Clamp(width + 28, 100, 65_536);
        }

        var max = (column + 1).ToString().Length;
        foreach (var row in _rows.Take(200))
            if (column < row.Count) max = Math.Max(max, row[column].Length);
        return Math.Clamp(max * 11 + 28, 100, 420);
    }

    private static int MeasureCellWidth(string text, Font font) =>
        TextRenderer.MeasureText(text.Length == 0 ? " " : text, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

    private void GridOnRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        var text = (e.RowIndex + 1).ToString();
        TextRenderer.DrawText(e.Graphics, text, _grid.Font,
            new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth - 8, e.RowBounds.Height),
            SystemColors.ControlText, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void UpdateStatus()
    {
        if (_grid.RowCount == 0) { _statusLabel.Text = "0 行"; return; }
        _statusLabel.Text = $"行 {CurrentRow() + 1:N0}/{_grid.RowCount:N0}  列 {CurrentColumn() + 1:N0}/{_grid.ColumnCount:N0}  " +
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
