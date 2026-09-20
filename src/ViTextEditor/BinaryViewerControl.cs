using System.Text;

namespace ViTextEditor;

internal sealed class BinaryViewerControl : UserControl
{
    private const int BytesPerRow = 16;
    private const int MaxCachedRows = 1024;

    private readonly FastDataGridView _grid = new();
    private readonly Label _header = new();
    private readonly Dictionary<int, BinaryRow> _rowCache = new();
    private FileStream? _stream;
    private long _length;
    private string? _filePath;

    public BinaryViewerControl()
    {
        Dock = DockStyle.Fill;
        Visible = false;

        _header.Dock = DockStyle.Top;
        _header.Height = 28;
        _header.Padding = new Padding(8, 5, 8, 0);
        _header.Text = "BINARY  16 bytes/row   OFFSET | HEX | ASCII";

        _grid.Dock = DockStyle.Fill;
        _grid.VirtualMode = true;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Font = new Font("Consolas", 10f);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = Math.Max(20, _grid.Font.Height + 5);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Offset",
            HeaderText = "OFFSET",
            Width = 120,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Hex",
            HeaderText = "HEX (00-FF)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 75,
            MinimumWidth = 420,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Ascii",
            HeaderText = "ASCII / TEXT",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 25,
            MinimumWidth = 170,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _grid.CellValueNeeded += GridOnCellValueNeeded;

        Controls.Add(_grid);
        Controls.Add(_header);
    }

    public string StatusText => _filePath is null
        ? "BINARY"
        : $"BINARY  {_length:N0} bytes";

    public bool LoadFile(string path, out string? error)
    {
        error = null;
        try
        {
            CloseFile();
            var fullPath = Path.GetFullPath(path);
            _stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.RandomAccess);
            _length = _stream.Length;
            _filePath = fullPath;
            _rowCache.Clear();

            var rowCount = (_length + BytesPerRow - 1) / BytesPerRow;
            _grid.RowCount = (int)Math.Min(int.MaxValue, rowCount);
            _header.Text = $"BINARY  16 bytes/row   OFFSET | HEX | ASCII    {Path.GetFileName(fullPath)}    {_length:N0} bytes";
            if (_grid.RowCount > 0)
            {
                _grid.FirstDisplayedScrollingRowIndex = 0;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
            Invalidate(true);
            return true;
        }
        catch (Exception ex)
        {
            CloseFile();
            error = ex.Message;
            return false;
        }
    }

    public void RefreshCurrentFile()
    {
        if (_filePath is null)
        {
            return;
        }

        var path = _filePath;
        LoadFile(path, out _);
    }

    public void CloseFile()
    {
        _grid.RowCount = 0;
        _rowCache.Clear();
        _stream?.Dispose();
        _stream = null;
        _length = 0;
        _filePath = null;
        _header.Text = "BINARY  16 bytes/row   OFFSET | HEX | ASCII";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Dispose();
            _grid.Dispose();
            _header.Dispose();
        }
        base.Dispose(disposing);
    }

    private void GridOnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (_stream is null || e.RowIndex < 0)
        {
            return;
        }

        var row = GetRow(e.RowIndex);
        e.Value = e.ColumnIndex switch
        {
            0 => row.Offset,
            1 => row.Hex,
            2 => row.Text,
            _ => string.Empty
        };
    }

    private BinaryRow GetRow(int rowIndex)
    {
        if (_rowCache.TryGetValue(rowIndex, out var cached))
        {
            return cached;
        }

        if (_stream is null)
        {
            return default;
        }

        var offset = (long)rowIndex * BytesPerRow;
        Span<byte> bytes = stackalloc byte[BytesPerRow];
        var count = RandomAccess.Read(_stream.SafeFileHandle, bytes, offset);
        var slice = bytes[..count];

        var hex = new StringBuilder(BytesPerRow * 3 - 1);
        var text = new StringBuilder(BytesPerRow);
        for (var i = 0; i < BytesPerRow; i++)
        {
            if (i < count)
            {
                if (i > 0) hex.Append(' ');
                hex.Append(slice[i].ToString("X2"));
                var value = slice[i];
                text.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
            }
            else
            {
                if (i > 0) hex.Append(' ');
                hex.Append("  ");
                text.Append(' ');
            }
        }

        var result = new BinaryRow(offset.ToString(_length > uint.MaxValue ? "X16" : "X8"), hex.ToString(), text.ToString());
        if (_rowCache.Count >= MaxCachedRows)
        {
            _rowCache.Clear();
        }
        _rowCache[rowIndex] = result;
        return result;
    }

    private readonly record struct BinaryRow(string Offset, string Hex, string Text);

    private sealed class FastDataGridView : DataGridView
    {
        public FastDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
